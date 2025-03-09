using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CopeSeetheMeld.Data;

namespace CopeSeetheMeld;

public class ProcessGearset(Gearset goal) : AutoTask
{
    private record class FoundItem(Pointer<InventoryItem> Item)
    {
        public unsafe uint Slot => Item.Value->GetSlot();
        public unsafe int SlotCount => Plugin.LuminaRow<Item>(Item.Value->ItemId).MateriaSlotCount;
        public unsafe int MateriaCount => Item.Value->GetMateriaCount();
        public unsafe InventoryType Container => Item.Value->GetInventoryType();
        public static unsafe implicit operator InventoryItem*(FoundItem f) => f.Item.Value;

        public override unsafe string ToString() => $"{Container}/{Slot}";
    }

    protected override async Task Execute()
    {
        List<FoundItem> foundItems = [];
        foreach (var (itemSlot, desiredItem) in goal.Slots)
        {
            var currentItem = FindItem(desiredItem, itemSlot, foundItems);
            if (currentItem == null)
            {
                Log($"Unable to find item for {desiredItem}, skipping");
                continue;
            }

            foundItems.Add(currentItem);
            await MeldItem(desiredItem, currentItem);
        }
    }

    private static IEnumerable<InventoryType> GetUsableInventories(ItemType ty)
    {
        yield return InventoryType.Inventory1;
        yield return InventoryType.Inventory2;
        yield return InventoryType.Inventory3;
        yield return InventoryType.Inventory4;
        yield return InventoryType.EquippedItems;

        yield return ty switch
        {
            ItemType.Weapon => InventoryType.ArmoryMainHand,
            ItemType.Head => InventoryType.ArmoryHead,
            ItemType.Body => InventoryType.ArmoryBody,
            ItemType.Hands => InventoryType.ArmoryHands,
            ItemType.Legs => InventoryType.ArmoryLegs,
            ItemType.Feet => InventoryType.ArmoryFeets,
            ItemType.Offhand => InventoryType.ArmoryOffHand,
            ItemType.Ears => InventoryType.ArmoryEar,
            ItemType.Neck => InventoryType.ArmoryNeck,
            ItemType.Wrists => InventoryType.ArmoryWrist,
            ItemType.RingL or ItemType.RingR => InventoryType.ArmoryRings,
            _ => InventoryType.Invalid
        };
    }

    private static FoundItem? FindItem(ItemSlot slot, ItemType itemType, List<FoundItem> usedSlots)
    {
        unsafe
        {
            var im = InventoryManager.Instance();

            foreach (var inventoryType in GetUsableInventories(itemType))
            {
                var cnt = im->GetInventoryContainer(inventoryType);
                for (var i = 0; i < cnt->Size; i++)
                {
                    if (usedSlots.Any(s => s.Container == inventoryType && s.Slot == i))
                        continue;

                    var item = cnt->GetInventorySlot(i);
                    if (item == null)
                        continue;

                    if (item->ItemId == slot.Id && item->IsHighQuality() == slot.HighQuality)
                        return new(item);
                }
            }
        }

        return null;
    }

    private List<Mat> GetCurrentMateria(FoundItem item)
    {
        unsafe
        {
            List<Mat> cur = [];
            for (byte i = 0; i < item.Item.Value->GetMateriaCount(); i++)
                cur.Add(new(item.Item.Value->GetMateriaId(i), item.Item.Value->GetMateriaGrade(i)));
            return cur;
        }
    }

    private async Task MeldItem(ItemSlot want, FoundItem have)
    {
        var normalSlotCount = have.SlotCount;

        var wantMat = want.Materia.TakeWhile(m => m > 0).Select(m => GetMateriaById(m)!);
        var haveMat = GetCurrentMateria(have);

        static Dictionary<Mat, int> groupCnt(IEnumerable<Mat> items) => items.GroupBy(v => v).Select(v => (v.Key, v.Count())).ToDictionary();

        var wantDict = groupCnt(wantMat.Take(normalSlotCount));
        var haveDict = groupCnt(haveMat.Take(normalSlotCount));

        for (var i = 0; i < Math.Min(normalSlotCount, haveMat.Count); i++)
        {
            var cur = haveMat[i];

            // order is irrelevant, just need to make sure that the materia quantities in non-overmeld slots match
            if (wantDict.TryGetValue(cur, out var value))
            {
                wantDict[cur] = value - 1;
                if (wantDict[cur] == 0)
                    wantDict.Remove(cur);
            }
            else
            {
                // retrieve materia until slot is empty
                await EnsureSlotEmpty(have, i);
                // do regular melds
                foreach (var m in wantDict.SelectMany(k => Enumerable.Repeat(k.Key, k.Value)))
                    await MeldOne(have, m);
                break;
            }
        }

        // do overmelds
        foreach (var w in wantMat.Skip(normalSlotCount))
            await MeldOne(have, w);
    }

    private async Task EnsureSlotEmpty(FoundItem foundItem, int slotIndex)
    {
        var cnt = foundItem.MateriaCount;
        Log($"Removing {Math.Max(0, cnt - slotIndex)} materia from {foundItem}");
    }

    private async Task MeldOne(FoundItem foundItem, Mat m)
    {
        Log($"Melding {m} onto {foundItem}");
    }
}
