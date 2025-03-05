using CopeSeetheMeld;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CopeSeetheMeld.Windows;

public partial class MainWindow : Window, IDisposable
{
    private Plugin plugin;
    private Configuration Config => plugin.Configuration;
    private Task? importTask = null;
    private readonly HttpClient httpClient;
    private string message = "";
    private readonly JsonSerializerOptions jop = new() { IncludeFields = true };

    private GearsetBase<Gearsets.ItemStatus>? gearsetDetail = null;

    private readonly UldWrapper materiaUld;

    private readonly ReadOnlyCollection<IDalamudTextureWrap?> materiaIcons;

    public MainWindow(Plugin plugin)
        : base("CSM", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;

        httpClient = new();

        materiaUld = Plugin.PluginInterface.UiBuilder.LoadUld("ui/uld/ItemDetail.uld");
        int[] iconParts = [6, 5, 4, 3, 21, 23, 25, 27, 29, 31, 33, 35];
        int[] iconOvermeldParts = [20, 19, 18, 17, 22, 24, 26, 28, 30, 32, 34, 36];

        materiaIcons = Enumerable.Concat(iconParts, iconOvermeldParts).Select(p => materiaUld.LoadTexturePart("ui/uld/ItemDetail_hr1.tex", p)).ToList().AsReadOnly();
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Import from clipboard"))
            DoImport(ImGui.GetClipboardText());

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Teamcraft/Etro URL");

        if (importTask?.IsCompleted ?? false)
        {
            if (importTask.IsFaulted)
                message = importTask.Exception.Message;
            else if (importTask.IsCanceled)
                message = "Import canceled.";
            else
            {
                message = "";
                importTask = null;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Delete all"))
        {
            Config.Gearsets.Clear();
            Config.SelectedGearset = null;
        }

        if (message != "")
            ImGui.Text(message);

        if (ImGui.BeginChild("Left", new Vector2(150 * ImGuiHelpers.GlobalScale, -1), false))
        {
            DrawSidebar();
            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGui.BeginChild("Right", new Vector2(-1, -1), false, ImGuiWindowFlags.NoSavedSettings))
        {
            if (Config.SelectedGearset is string s && Config.Gearsets.TryGetValue(s, out var gearset))
                DrawGearset(gearset);
            ImGui.EndChild();
        }
    }

    private void DrawSidebar()
    {
        foreach (var g in Config.Gearsets.Keys)
            if (ImGui.Selectable(g, g == Config.SelectedGearset))
                Config.SelectedGearset = g;
    }

    private void DrawGearset(Gearset gs)
    {
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
        DrawEquip(gs[ItemType.Weapon]);
        DrawEquip(gs[ItemType.Offhand]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawEquip(gs[ItemType.Head]);
        DrawEquip(gs[ItemType.Ears]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawEquip(gs[ItemType.Body]);
        DrawEquip(gs[ItemType.Neck]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawEquip(gs[ItemType.Hands]);
        DrawEquip(gs[ItemType.Wrists]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawEquip(gs[ItemType.Legs]);
        DrawEquip(gs[ItemType.RingL]);

        ImGui.TableNextRow(ImGuiTableRowFlags.None, 80);
        DrawEquip(gs[ItemType.Feet]);
        DrawEquip(gs[ItemType.RingR]);

        ImGui.EndTable();

        if (ImGui.Button("Meld it!"))
            Meld.Start(Gearsets.Check(gs));

        foreach (var m in Meld.Messages)
            ImGui.Text(m);
    }

    [GeneratedRegex(@"https?:\/\/etro\.gg\/gearset\/([^/]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Etro();

    private void DoImport(string text)
    {
        var m = Etro().Match(text);
        if (m.Success)
        {
            importTask = DoEtroImport(m.Groups[1].Value);
            return;
        }

        message = "Unrecognized URL or JSON";
    }

    public class EtroGearset
    {
        public required string name;
        public required Dictionary<string, Dictionary<string, uint>> materia;
        public uint? weapon;
        public uint? head;
        public uint? body;
        public uint? hands;
        public uint? legs;
        public uint? feet;
        public uint? offHand;
        public uint? ears;
        public uint? neck;
        public uint? wrists;
        public uint? fingerL;
        public uint? fingerR;
    }

    private async Task DoEtroImport(string gearsetId)
    {
        message = $"Importing Etro gearset {gearsetId}";

        var contents = await httpClient.GetStringAsync($"https://etro.gg/api/gearsets/{gearsetId}");
        var gs = JsonSerializer.Deserialize<EtroGearset>(contents, jop);

        if (gs == null)
            throw new Exception("Unexpected response from Etro API");

        var newgs = new Gearset() { Name = gs.name };

        newgs[ItemType.Weapon] = EtroToItem(gs, gs.weapon);
        newgs[ItemType.Head] = EtroToItem(gs, gs.head);
        newgs[ItemType.Body] = EtroToItem(gs, gs.body);
        newgs[ItemType.Hands] = EtroToItem(gs, gs.hands);
        newgs[ItemType.Legs] = EtroToItem(gs, gs.legs);
        newgs[ItemType.Feet] = EtroToItem(gs, gs.feet);
        newgs[ItemType.Offhand] = EtroToItem(gs, gs.offHand);
        newgs[ItemType.Ears] = EtroToItem(gs, gs.ears);
        newgs[ItemType.Neck] = EtroToItem(gs, gs.neck);
        newgs[ItemType.Wrists] = EtroToItem(gs, gs.wrists);
        newgs[ItemType.RingL] = EtroToItem(gs, gs.fingerL, "L");
        newgs[ItemType.RingR] = EtroToItem(gs, gs.fingerR, "R");

        Config.Gearsets.Add(newgs.Name, newgs);
        Config.SelectedGearset = newgs.Name;
    }

    internal static ItemSlot EtroToItem(EtroGearset egs, uint? itemId, string keyExtra = "")
    {
        if (itemId == null)
            return new ItemSlot() { Id = 0 };

        var slot = new ItemSlot() { Id = itemId.Value };

        var materiaSlotKey = $"{itemId}{keyExtra}";

        if (egs.materia.TryGetValue(materiaSlotKey, out var materia))
        {
            foreach (var (k, id) in materia)
            {
                switch (k)
                {
                    case "1":
                        slot.Materia1 = id;
                        break;
                    case "2":
                        slot.Materia2 = id;
                        break;
                    case "3":
                        slot.Materia3 = id;
                        break;
                    case "4":
                        slot.Materia4 = id;
                        break;
                    case "5":
                        slot.Materia5 = id;
                        break;
                }
            }
        }

        return slot;
    }

    internal void DrawEquip(ItemSlot slot, int iconSize = 64)
    {
        ImGui.TableNextColumn();

        if (slot.Id == 0 || Plugin.Item(slot.Id) is not Item it)
        {
            ImGui.TableNextColumn();
            return;
        }

        var maxSlots = it.MateriaSlotCount;

        var ic = Plugin.TextureProvider.GetFromGameIcon((uint)it.Icon)?.GetWrapOrEmpty();
        if (ic != null)
            ImGui.Image(ic.ImGuiHandle, new(iconSize, iconSize));

        ImGui.TableNextColumn();
        ImGui.Text(it.Name.ToString());

        ImGui.Text("");

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        foreach (var (i, m) in Enumerable.Range(1, 5).Zip(slot.Materia))
            DrawMateria(m, i > maxSlots);
        ImGui.PopStyleVar();
    }

    internal void DrawMateria(uint itemId, bool over)
    {
        if (itemId == 0)
            return;

        var materia = LookupMateria(itemId);
        if (materia == null)
            return;

        var grade = materia.Value.Grade;

        if (over)
            grade += 12;

        if (materiaIcons[grade] is IDalamudTextureWrap t)
        {
            ImGui.SameLine();
            ImGui.Image(t.ImGuiHandle, new(32, 32));

            var it = Plugin.DataManager.Excel.GetSheet<Item>()?.GetRow(itemId);
            if (it != null && ImGui.IsItemHovered())
                ImGui.SetTooltip($"{it.Value.Name} ({materia.Value.Materia.BaseParam.Value!.Name} +{materia.Value.Materia.Value[materia.Value.Grade]})");
        }
    }

    internal static (Materia Materia, int Grade)? LookupMateria(uint itemId)
    {
        foreach (var materia in Plugin.DataManager.GameData.GetExcelSheet<Materia>()!.Where(x => x.Item[0].RowId > 0))
        {
            var grade = materia.Item.ToList().FindIndex(y => y.RowId == itemId);
            if (grade >= 0)
                return (materia, grade);
        }

        return null;
    }
}
