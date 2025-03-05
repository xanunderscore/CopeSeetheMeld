using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using SamplePlugin;
using System.Collections.Generic;

namespace CopeSeetheMeld;

public class Gearsets
{
    public record struct InventoryLocation(InventoryType InventoryType, int Location);

    public record struct ItemStatus(ItemSlot GearsetItem, InventoryLocation? Source = null, uint Materia1 = 0, uint Materia2 = 0, uint Materia3 = 0, uint Materia4 = 0, uint Materia5 = 0)
    {
        public IEnumerable<uint> Materia
        {
            readonly get => [Materia1, Materia2, Materia3, Materia4, Materia5];
            set
            {
                Materia1 = Materia2 = Materia3 = Materia4 = Materia5 = 0;
                var en = value.GetEnumerator();
                if (!en.MoveNext())
                    return;
                Materia1 = en.Current;
                if (!en.MoveNext())
                    return;
                Materia2 = en.Current;
                if (!en.MoveNext())
                    return;
                Materia3 = en.Current;
                if (!en.MoveNext())
                    return;
                Materia4 = en.Current;
                if (!en.MoveNext())
                    return;
                Materia5 = en.Current;
            }
        }
    }

    private static InventoryType[] AllInventories = [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
    ];

    public static GearsetBase<ItemStatus> Check(Gearset g)
    {
        var meldset = new GearsetBase<ItemStatus>() { Name = g.Name };

        List<InventoryLocation> alreadyUsedSlots = [];

        foreach (var (slot, it) in g)
        {
            var st = GetItemStatus(it, alreadyUsedSlots);
            if (st.Source != null)
                alreadyUsedSlots.Add(st.Source.Value);

            meldset[slot] = st;
        }

        return meldset;
    }

    private static unsafe ItemStatus GetItemStatus(ItemSlot item, List<InventoryLocation> skippedSlots)
    {
        var st = new ItemStatus(item);
        if (item.Id == 0)
            return st;

        var im = InventoryManager.Instance();

        foreach (var ty in AllInventories)
        {
            var container = im->GetInventoryContainer(ty);
            if (!container->IsLoaded)
            {
                Plugin.Log.Warning($"unable to load inventory {ty}, it will be skipped");
                continue;
            }

            for (var i = 0; i < container->Size; i++)
            {
                var curLocation = new InventoryLocation(ty, i);
                if (skippedSlots.Contains(curLocation))
                    continue;

                var slot = container->GetInventorySlot(i);
                if (slot->ItemId == item.Id)
                    return MakeItemStatus(st, curLocation, slot);
            }
        }

        return st;
    }

    private static unsafe ItemStatus MakeItemStatus(ItemStatus st, InventoryLocation source, InventoryItem* item)
    {
        st.Source = source;
        List<uint> materias = [];
        for (var i = 0; i < 5; i++)
        {
            var haveMateria = item->Materia[i];
            if (haveMateria == 0)
            {
                materias.Add(0);
                continue;
            }
            var haveMateriaGrade = item->MateriaGrades[i];
            var matSheet = Plugin.DataManager.GetExcelSheet<Materia>()!;
            var matType = matSheet.GetRow(haveMateria)!;
            var matItem = matType.Item[item->MateriaGrades[i]];
            materias.Add(matItem.RowId);
        }
        st.Materia = materias;
        return st;
    }
}
