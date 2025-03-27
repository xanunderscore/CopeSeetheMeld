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

    private static string MakeTeamcraftSetName()
    {
        if (!Plugin.Config.Gearsets.ContainsKey("Teamcraft Import"))
            return "Teamcraft Import";

        var i = 1;
        while (Plugin.Config.Gearsets.ContainsKey($"Teamcraft Import ({i})"))
            i++;

        return $"Teamcraft Import ({i})";
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
}
