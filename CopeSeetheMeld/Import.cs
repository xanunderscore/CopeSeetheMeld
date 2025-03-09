using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CopeSeetheMeld;

public partial class Import(string input) : AutoTask
{
    private readonly HttpClient client = new();
    private readonly JsonSerializerOptions jop = new() { IncludeFields = true };

    [GeneratedRegex(@"https?:\/\/etro\.gg\/gearset\/([^/]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternEtro();

    [GeneratedRegex(@"(?:https?:\/\/xivgear\.app\/\?page=sl%7C|https?:\/\/api\.xivgear\.app\/shortlink\/)([a-zA-Z0-9-]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternXIVG();

    protected override async Task Execute()
    {
        // teamcraft export is just markdown
        if (input.StartsWith("**"))
        {
            Status = "Importing from TC";
            ImportTeamcraft(input);
            return;
        }

        var m1 = PatternEtro().Match(input);
        if (m1.Success)
        {
            Status = "Importing from Etro";
            await ImportEtro(m1.Groups[1].Value);
            return;
        }

        var m2 = PatternXIVG().Match(input);
        if (m2.Success)
        {
            Status = "Importing from xivgear";
            await ImportXIVG(m2.Groups[1].Value);
            return;
        }

        Error("Unrecognized input");
    }

    private async Task ImportEtro(string gearsetId)
    {
        Log("Importing from etro");
    }

    public record class XGSet
    {
        public required string name;
        public required Dictionary<string, XGItem> items;
    }

    public record class XGItem
    {
        public uint id;
        public required List<XGId> materia;
    }

    public record class XGId
    {
        public uint id;
    }

    private async Task ImportXIVG(string shortcode)
    {
        var contents = await client.GetStringAsync($"https://api.xivgear.app/shortlink/{shortcode}");
        var xgs = JsonSerializer.Deserialize<XGSet>(contents, jop) ?? throw new Exception("Bad response from server");

        var gs = Gearset.Create(xgs.name);

        void mk(ItemType ty, string field)
        {
            if (!xgs.items.TryGetValue(field, out var item))
                return;

            var slot = ItemSlot.Create(item.id);
            for (var i = 0; i < Math.Min(5, item.materia.Count); i++)
                slot.Materia[i] = item.materia[i].id;

            gs[ty] = slot;
        }

        mk(ItemType.Weapon, "Weapon");
        mk(ItemType.Head, "Head");
        mk(ItemType.Body, "Body");
        mk(ItemType.Hands, "Hand");
        mk(ItemType.Legs, "Legs");
        mk(ItemType.Feet, "Feet");
        mk(ItemType.Offhand, "OffHand");
        mk(ItemType.Ears, "Ears");
        mk(ItemType.Neck, "Neck");
        mk(ItemType.Wrists, "Wrist");
        mk(ItemType.RingL, "RingLeft");
        mk(ItemType.RingR, "RingRight");

        Plugin.Config.Gearsets.Add(xgs.name, gs);
    }

    private static void ImportTeamcraft(string markdown)
    {
        Dictionary<string, uint> materiaIds = [];

        var name = MakeTeamcraftSetName();

        var gs = Gearset.Create(name);

        var lines = markdown.Split(Environment.NewLine);

        var i = 0;
        while (true)
        {
            var itemName = lines[i].Trim('*');
            if (itemName.Length == 0)
                break;

            var hq = false;

            if (itemName.EndsWith(" HQ"))
            {
                hq = true;
                itemName = itemName[..^3];
            }

            i += 2;
            List<string> materia = [];
            while (lines[i].StartsWith('-'))
            {
                materia.Add(lines[i][2..]);
                i++;
            }

            if (Data.GetItemByName(itemName) is { } matchedRow)
            {
                var ty = GetItemEquipType(matchedRow);
                if (ty == ItemType.Invalid)
                    continue;

                if (ty == ItemType.RingL && gs[ItemType.RingL].Id != 0)
                    ty = ItemType.RingR;

                var slot = ItemSlot.Create(matchedRow.RowId, hq);
                foreach (var (m, ix) in materia.Select((m, i) => (m, i)))
                {
                    if (Data.GetItemByName(m) is { } matchedMateria)
                    {
                        materiaIds[m] = matchedMateria.RowId;
                        slot.Materia[ix] = matchedMateria.RowId;
                    }
                }
                gs[ty] = slot;
            }

            i++;
        }

        Plugin.Config.Gearsets.Add(name, gs);
    }

    private static readonly ItemType[] ItemTypesSheetOrder = [ItemType.Weapon, ItemType.Offhand, ItemType.Head, ItemType.Body, ItemType.Hands, ItemType.Invalid, ItemType.Legs, ItemType.Feet, ItemType.Ears, ItemType.Neck, ItemType.Wrists, ItemType.RingL, ItemType.RingR];

    private static ItemType GetItemEquipType(Item it)
    {
        var esc = Plugin.DataManager.Excel.GetSheet<RawRow>(null, "EquipSlotCategory").GetRowOrDefault(it.EquipSlotCategory.RowId);

        if (esc != null)
            for (var i = 0; i < ItemTypesSheetOrder.Length; i++)
                if (esc.Value.ReadInt8Column(i) == 1)
                    return ItemTypesSheetOrder[i];

        return ItemType.Invalid;
    }

    private static string MakeTeamcraftSetName()
    {
        if (!Plugin.Config.Gearsets.ContainsKey("Teamcraft Import"))
            return "Teamcraft Import";

        var i = 1;
        while (Plugin.Config.Gearsets.ContainsKey($"Teamcraft Import ({i})"))
            i++;

        return $"Teamcraft Import ({i})";
    }
}
