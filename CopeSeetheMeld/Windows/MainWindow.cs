using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace CopeSeetheMeld.Windows;

public partial class MainWindow : Window, IDisposable
{
    private static Configuration Config => Plugin.Config;
    private readonly Automation auto = new();
    private readonly UldWrapper materiaUld;
    private readonly ReadOnlyCollection<IDalamudTextureWrap?> materiaIcons;

    public MainWindow()
        : base("CSM", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        materiaUld = Plugin.PluginInterface.UiBuilder.LoadUld("ui/uld/ItemDetail.uld");
        int[] iconParts = [6, 5, 4, 3, 21, 23, 25, 27, 29, 31, 33, 35];
        int[] iconOvermeldParts = [20, 19, 18, 17, 22, 24, 26, 28, 30, 32, 34, 36];

        materiaIcons = Enumerable.Concat(iconParts, iconOvermeldParts).Select(p => materiaUld.LoadTexturePart("ui/uld/ItemDetail_hr1.tex", p)).ToList().AsReadOnly();
    }

    public void Dispose()
    {
        auto.Dispose();
        materiaUld.Dispose();
        foreach (var item in materiaIcons)
            item?.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        using (ImRaii.Disabled(!auto.Running))
            if (ImGui.Button("Stop current task"))
                auto.Stop();
        ImGui.SameLine();
        ImGui.TextUnformatted($"Status: {auto.CurrentTask?.Status ?? "idle"}");

        var ctrl = ImGui.GetIO().KeyCtrl;

        if (ImGui.Button("Import from clipboard"))
        {
            Plugin.Log.Debug($"importing {ImGui.GetClipboardText()}");
            auto.Start(new Import(ImGui.GetClipboardText()));
        }

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(FontAwesomeIcon.InfoCircle.ToIconString());

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"xivgear.app URL, etro.gg URL, or Teamcraft \"Copy gearset to clipboard\"");

        using (ImRaii.Disabled(!ctrl))
            if (ImGui.Button("Delete all"))
            {
                Config.Gearsets.Clear();
                Config.SelectedGearset = null;
            }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Child("Left", new Vector2(150 * ImGuiHelpers.GlobalScale, -1)))
            DrawSidebar();

        ImGui.SameLine();

        using (ImRaii.Child("Right", new Vector2(-1, -1), false, ImGuiWindowFlags.NoSavedSettings))
            if (Config.SelectedGearset is string s && Config.Gearsets.TryGetValue(s, out var gearset))
                DrawGearset(gearset);
    }

    private static void DrawSidebar()
    {
        foreach (var g in Config.Gearsets.Keys)
            if (ImGui.Selectable(g, g == Config.SelectedGearset))
                Config.SelectedGearset = Config.SelectedGearset == g ? null : g;
    }

    private void DrawGearset(Gearset gs)
    {
        var rename = gs.Name;
        if (ImGui.InputText("Name", ref rename, 255, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            var oldName = gs.Name;
            gs.Name = rename;
            Config.Gearsets.Add(rename, gs);
            Config.Gearsets.Remove(oldName);
            Config.SelectedGearset = rename;
        }

        using (ImRaii.Disabled(!ImGui.GetIO().KeyCtrl))
            if (ImGui.Button("Delete"))
            {
                Config.Gearsets.Remove(gs.Name);
                Config.SelectedGearset = null;
            }

        ImGui.BeginTable("items", 4, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingStretchProp);
        ImGui.TableSetupColumn("###iconsleft", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("###textleft");
        ImGui.TableSetupColumn("###iconsright", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("###textright");

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawItemSlot(gs[ItemType.Weapon]);
        DrawItemSlot(gs[ItemType.Offhand]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawItemSlot(gs[ItemType.Head]);
        DrawItemSlot(gs[ItemType.Ears]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawItemSlot(gs[ItemType.Body]);
        DrawItemSlot(gs[ItemType.Neck]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawItemSlot(gs[ItemType.Hands]);
        DrawItemSlot(gs[ItemType.Wrists]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawItemSlot(gs[ItemType.Legs]);
        DrawItemSlot(gs[ItemType.RingL]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawItemSlot(gs[ItemType.Feet]);
        DrawItemSlot(gs[ItemType.RingR]);

        ImGui.EndTable();

        if (ImGui.Button("Meld it!"))
            auto.Start(new ProcessGearset(gs));
    }

    private void DrawItemSlot(ItemSlot slot, int iconSize = 64)
    {
        ImGui.TableNextColumn();

        if (slot.Id == 0)
        {
            ImGui.TableNextColumn();
            return;
        }
        var it = Data.Item(slot.Id);

        var maxSlots = it.MateriaSlotCount;

        UI.Draw(it, slot.HighQuality);

        ImGui.TableNextColumn();
        ImGui.Text(it.Name.ToString());

        ImGui.Text("");

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        foreach (var (i, m) in Enumerable.Range(1, 5).Zip(slot.Materia))
            DrawMateria(m, i > maxSlots);
        ImGui.PopStyleVar();
    }

    private void DrawMateria(uint itemId, bool over)
    {
        if (itemId == 0)
            return;

        if (!Data.TryGetMateriaById(itemId, out var materia))
            return;

        var grade = materia.Grade;

        if (over)
            grade += 12;

        if (materiaIcons[grade] is IDalamudTextureWrap t)
        {
            ImGui.SameLine();
            ImGui.Image(t.ImGuiHandle, new(32, 32));

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"{Data.ItemName(itemId)} ({materia})");
            }
        }
    }
}
