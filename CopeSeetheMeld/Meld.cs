using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CopeSeetheMeld.Data;

namespace CopeSeetheMeld;

public class Meld(Gearset goal, bool doOvermeld = true, bool stopOnMissingItem = false, bool stopOnMissingMateria = false) : AutoCommon
{
    private readonly bool Overmeld = doOvermeld;

    protected override async Task Execute()
    {
        ErrorIf(Game.PlayerIsBusy, "Can't meld while occupied");

        List<ItemRef> foundItems = [];
        foreach (var (itemSlot, desiredItem) in goal.Slots)
        {
            var currentItem = FindItem(desiredItem, itemSlot, foundItems);
            if (currentItem == null)
            {
                if (stopOnMissingItem)
                    throw new ItemNotFoundException(desiredItem.Id, desiredItem.HighQuality);

                continue;
            }

            foundItems.Add(currentItem);
            try
            {
                await MeldItem(desiredItem, currentItem);
            }
            catch (MateriaNotFoundException)
            {
                if (stopOnMissingMateria)
                    throw;
            }
        }

        unsafe { AgentMateriaAttach.Instance()->Hide(); }
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

    private static ItemRef? FindItem(ItemSlot slot, ItemType itemType, List<ItemRef> usedSlots)
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

    private List<Mat> GetCurrentMateria(ItemRef item)
    {
        unsafe
        {
            List<Mat> cur = [];
            for (byte i = 0; i < item.Item.Value->GetMateriaCount(); i++)
                cur.Add(new(item.Item.Value->GetMateriaId(i), item.Item.Value->GetMateriaGrade(i)));
            return cur;
        }
    }

    private async Task MeldItem(ItemSlot want, ItemRef have)
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
                break;
            }
        }

        // do regular melds
        foreach (var m in wantDict.SelectMany(k => Enumerable.Repeat(k.Key, k.Value)))
            await MeldOne(have, m);

        // do overmelds
        if (Overmeld)
        {
            foreach (var w in wantMat.Skip(normalSlotCount))
                await MeldOne(have, w);
        }
        else
            Log($"Skipping overmelds for {have}");
    }

    private async Task MeldOne(ItemRef foundItem, Mat m)
    {
        Status = $"Melding {m} onto {foundItem}";

        await OpenAgent();
        await SelectItem(foundItem);
        await SelectMateria(m);
        await WaitWhile(() => !Game.PlayerIsMelding, "MeldStart");
        await WaitWhile(() => !Game.IsAddonActive("MateriaAttachDialog"), "AttachDialog");

        unsafe { Game.FireCallback("MateriaAttachDialog", [0, 0, 1], true); }

        await WaitWhile(() => Game.PlayerIsMelding, "MeldFinish", 10);
    }

    private async Task EnsureSlotEmpty(ItemRef foundItem, int slotIndex)
    {
        Status = $"Retrieving from {foundItem}";

        while (foundItem.MateriaCount > slotIndex)
        {
            await OpenAgent();
            await SelectItem(foundItem);

            unsafe { Game.AgentReceiveEvent(&AgentMateriaAttach.Instance()->AgentInterface, 4, [0, 1, 0, 0, 0]); }

            await WaitWhile(() => !Game.IsAddonActive("MateriaRetrieveDialog"), "RetrieveDialog");

            unsafe { Game.FireCallback("MateriaRetrieveDialog", [0], true); }

            await WaitWhile(() => Game.PlayerIsRetrieving, "RetrieveFinish", 10);
        }
    }
}
