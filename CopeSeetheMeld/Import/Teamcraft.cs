using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopeSeetheMeld.Import;

public partial class Import
{
    private static void ImportTeamcraft(string markdown)
    {
        Dictionary<string, uint> materiaIds = [];

        var name = MakeTeamcraftSetName();

        var gs = new Gearset(name);

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

                var slot = new ItemSlot(matchedRow.RowId, ty, hq);
                foreach (var (m, ix) in materia.Select((m, i) => (m, i)))
                {
                    if (Data.GetItemByName(m) is { } matchedMateria)
                    {
                        materiaIds[m] = matchedMateria.RowId;
                        slot.Materia[ix] = matchedMateria.RowId;
                    }
                }
                gs.Items.Add(slot);
            }

            i++;
        }

        Plugin.Config.GearsetList.Add(gs);
    }

    private static string MakeTeamcraftSetName()
    {
        var i = 0;

        string genName(int i) => i == 0 ? "Teamcraft Import" : $"Teamcraft Import ({i})";

        while (Plugin.Config.GearsetList.Any(g => g.Name == genName(i)))
            i++;

        return $"Teamcraft Import ({i})";
    }

    public static readonly ItemType[] ItemTypesSheetOrder = [ItemType.Weapon, ItemType.Offhand, ItemType.Head, ItemType.Body, ItemType.Hands, ItemType.Invalid, ItemType.Legs, ItemType.Feet, ItemType.Ears, ItemType.Neck, ItemType.Wrists, ItemType.Ring];

    public static ItemType GetItemEquipType(Item it)
    {
        var esc = Plugin.DataManager.Excel.GetSheet<RawRow>(null, "EquipSlotCategory").GetRowOrDefault(it.EquipSlotCategory.RowId);

        if (esc != null)
            for (var i = 0; i < ItemTypesSheetOrder.Length; i++)
                if (esc.Value.ReadInt8Column(i) == 1)
                    return ItemTypesSheetOrder[i];

        return ItemType.Invalid;
    }
}
