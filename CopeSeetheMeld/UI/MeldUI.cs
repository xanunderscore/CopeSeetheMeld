using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CopeSeetheMeld.UI;

internal class MeldUI : IDisposable
{
    private static Configuration Config => Plugin.Config;
    private readonly UldWrapper materiaUld = Plugin.PluginInterface.UiBuilder.LoadUld("ui/uld/ItemDetail.uld");
    private readonly ReadOnlyCollection<IDalamudTextureWrap?> materiaIcons;
    private readonly MeldOptions meldOptions = Config.LastUsedOptions;
    private readonly Automation auto;

    private bool popupOpen;
    private MeldLog? meldLog;

    public MeldUI(Automation auto)
    {
        this.auto = auto;
        int[] iconParts = [6, 5, 4, 3, 21, 23, 25, 27, 29, 31, 33, 35];
        int[] iconOvermeldParts = [20, 19, 18, 17, 22, 24, 26, 28, 30, 32, 34, 36];

        materiaIcons = iconParts.Concat(iconOvermeldParts).Select(p => materiaUld.LoadTexturePart("ui/uld/ItemDetail_hr1.tex", p)).ToList().AsReadOnly();
    }

    public void Dispose()
    {
        materiaUld.Dispose();
        foreach (var item in materiaIcons)
            item?.Dispose();
    }

    public void Draw()
    {
        ImGui.Spacing();

        using (ImRaii.Child("Left", new Vector2(250 * ImGuiHelpers.GlobalScale, -1)))
            DrawSidebar();

        ImGui.SameLine();

        using (ImRaii.Child("Right", new Vector2(-1, -1), false, ImGuiWindowFlags.NoSavedSettings))
            if (Config.GetSelected() is { } gs)
                DrawGearset(Config.SelectedIndex, gs);
    }

    private void DrawSidebar()
    {
        var ctrl = ImGui.GetIO().KeyCtrl;

        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
        {
            Plugin.Log.Debug($"importing {ImGui.GetClipboardText()}");
            auto.Start(new Import.Import(ImGui.GetClipboardText()));
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"xivgear.app URL, etro.gg URL, or Teamcraft \"Copy gearset to clipboard\"");

        ImGui.SameLine();

        using (ImRaii.Disabled(!ctrl || Config.SelectedIndex == -1))
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            {
                Config.GearsetList.RemoveAt(Config.SelectedIndex);
                Config.SelectedIndex = -1;
            }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Delete selected gearset (hold CTRL)");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        for (var i = 0; i < Config.GearsetList.Count; i++)
            if (ImGui.Selectable(Config.GearsetList[i].Name, Config.SelectedIndex == i))
            {
                meldLog = null;
                Config.SelectedIndex = Config.SelectedIndex == i ? -1 : i;
            }
    }

    private void DrawGearset(int index, Gearset gs)
    {
        DrawLogsPopup();
        DrawConfigurePopup();

        var rename = gs.Name;
        if (ImGui.InputText("Name", ref rename, 255, ImGuiInputTextFlags.EnterReturnsTrue))
            Config.Rename(index, rename);

        ImGui.Dummy(new(0, 12));

        if (ImGui.Button("Add DoH/DoL tools for other jobs"))
        {
            gs.Items.AddRange(Data.GetMissingTools(gs));
            gs.Sort();
        }

        using (ImRaii.Disabled(Game.PlayerIsBusy))
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Meld!"))
            {
                Plugin.Config.LastUsedOptions = meldOptions;
                meldLog = new();
                auto.Start(new Meld(gs, meldOptions, meldLog));
            }

        if (ImGui.IsItemHovered() && meldOptions.DryRun)
            ImGui.SetTooltip("Dry Run mode is active, so melds will only be logged, not actually performed.");

        ImGui.SameLine();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Cog, "Options"))
            ImGui.OpenPopup("Meld options");

        if (auto.LastError is { } e)
            foreach (var inner in e.InnerExceptions)
                ImGui.TextUnformatted(inner.Message);

        if (meldLog is { Done: true })
        {
            if (ImGui.Button("Show log"))
            {
                popupOpen = true;
                ImGui.OpenPopup("###showlog");
            }
            ImGui.SameLine();
            if (ImGui.Button("Clear log"))
            {
                meldLog = null;
            }
        }

        ImGui.Dummy(new(0, 12));

        ImGui.BeginTable("items", 3, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingStretchProp);
        ImGui.TableSetupColumn("###icons", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("###text");
        ImGui.TableSetupColumn("###materia");

        foreach (var item in gs.Items)
            DrawItemSlot(item);

        ImGui.EndTable();
    }

    private void DrawItemSlot(ItemSlot slot)
    {
        if (slot.Id == 0)
            return;

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 44);
        ImGui.TableNextColumn();
        var it = Data.Item(slot.Id);

        var maxSlots = it.MateriaSlotCount;

        UI.Draw(it, slot.HighQuality);

        ImGui.TableNextColumn();
        ImGui.Text(it.Name.ToString());

        ImGui.TableNextColumn();

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0)))
            foreach (var (i, m) in Enumerable.Range(1, 5).Zip(slot.Materia))
                DrawMateria(m, i > maxSlots);
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
            ImGui.Image(t.ImGuiHandle, new(32, 32));
            ImGui.SameLine();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{Data.ItemName(itemId)} ({materia})");
        }
    }

    private void DrawLogsPopup()
    {
        if (meldLog == null)
            return;

        using var p = ImRaii.PopupModal("###showlog", ref popupOpen, ImGuiWindowFlags.NoScrollbar);
        if (!p)
            return;

        ImGui.SetNextItemWidth(-1);
        using (ImRaii.ListBox("###modalscroll"))
            foreach (var act in meldLog.Actions)
                ImGui.TextUnformatted(act);

        var materiaOrdered = meldLog.MateriaUsed.OrderBy(m => (m.Key.Id, -m.Key.Grade));

        using (ImRaii.Table("###modaltable", 2))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableHeadersRow();


            foreach (var (m, cnt) in materiaOrdered)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{m.Item.Name}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{cnt}");
            }
        }

        if (ImGui.Button("Copy materia to clipboard"))
        {
            var sb = new StringBuilder();
            foreach (var (m, cnt) in materiaOrdered)
                sb.AppendLine($"- {m.Item.Name} ({m}) x{cnt}");
            ImGui.SetClipboardText(sb.ToString());
        }

        if (ImGui.Button("Close"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawConfigurePopup()
    {
        using var popup = ImRaii.Popup("Meld options");
        if (!popup.Success)
            return;

        meldOptions.Draw();
    }
}
