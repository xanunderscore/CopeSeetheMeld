using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CopeSeetheMeld.Data;

namespace CopeSeetheMeld;

public class Meld(Gearset goal, MeldOptions opts, MeldLog? log) : AutoTask
{
    protected override async System.Threading.Tasks.Task Execute()
    {
        ErrorIf(Game.PlayerIsBusy, "Can't meld while occupied");

        using var x = new OnDispose(() =>
        {
            unsafe
            {
                AgentMateriaAttach.Instance()->Hide();
            }
        });

        List<ItemRef> foundItems = [];
        foreach (var (itemSlot, desiredItem) in goal.Slots)
        {
            var currentItem = FindItem(desiredItem, itemSlot, foundItems);
            if (currentItem == null)
            {
                if (opts.StopOnMissingItem == MeldOptions.StopBehavior.Stop)
                    throw new ItemNotFoundException(desiredItem.Id, desiredItem.HighQuality);

                continue;
            }

            foundItems.Add(currentItem);
            try
            {
                await MeldItem(desiredItem, currentItem);
            }
            catch (MateriaNotFoundException m)
            {
                log?.ReportError(m);
                if (opts.StopOnMissingMateria == MeldOptions.StopBehavior.Stop)
                    throw;
            }
        }

        log?.Finish();
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

    private async System.Threading.Tasks.Task MeldItem(ItemSlot want, ItemRef have)
    {
        var shouldContinue = await MeldNormal(want, have);
        if (!shouldContinue)
            return;

        var normalSlotCount = have.NormalSlotCount;

        var wantMat = want.Materia.TakeWhile(m => m > 0).Select(m => GetMateriaById(m)!).ToList();
        var haveMat = GetCurrentMateria(have).ToList();

        for (var slot = normalSlotCount; slot < wantMat.Count; slot++)
        {
            var curMateria = have.GetMateria(slot);
            var wantMateria = wantMat[slot];

            if (curMateria != wantMateria)
            {
                if (!opts.Overmeld)
                {
                    Log($"Skipping overmelds for {have}");
                    return;
                }
                if (curMateria.Id != 0)
                    await EnsureSlotEmpty(have, slot);

                await MeldOne(have, wantMateria, slot - normalSlotCount);
            }
        }
    }

    private async Task<bool> MeldNormal(ItemSlot want, ItemRef have)
    {
        var normalSlotCount = have.NormalSlotCount;

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
                if (opts.Mode == MeldOptions.SpecialMode.MeldOnly)
                {
                    Log($"Retrieval needed for slot {i}");
                    return false;
                }

                // retrieve materia until slot is empty
                await EnsureSlotEmpty(have, i);
                break;
            }
        }

        if (opts.Mode == MeldOptions.SpecialMode.RetrieveOnly)
        {
            Log($"Done with retrievals, exiting");
            return false;
        }

        // do regular melds
        foreach (var m in wantDict.SelectMany(k => Enumerable.Repeat(k.Key, k.Value)))
            await MeldOne(have, m);

        return true;
    }

    private async System.Threading.Tasks.Task MeldOne(ItemRef foundItem, Mat m, int overmeldSlot = -1)
    {
        Status = $"Melding {m} onto {foundItem}";

        var materiaAmount = 1;

        if (overmeldSlot >= 0)
        {
            var chanceRow = Plugin.LuminaRow<MateriaGrade>(m.Grade);
            var chances = foundItem.IsHQ ? chanceRow.OvermeldHQPercent : chanceRow.OvermeldNQPercent;

            var confidence = opts.MeldConfidence * 0.01f;
            var chanceWhole = chances[overmeldSlot];
            var chance = chanceWhole * 0.01f;

            materiaAmount = Math.Max((int)MathF.Ceiling(MathF.Log(1 - confidence) / MathF.Log(1 - chance)), 1);

            Status = $"{Status} ({chanceWhole}% success rate)";
        }

        log?.UseMateria(m, materiaAmount);
        log?.Report(Status);

        if (opts.DryRun)
            return;

        await OpenAgent();
        await SelectItem(foundItem);
        await SelectMateria(m);
        await WaitWhile(() => !Game.PlayerIsMelding, "MeldStart");
        await WaitWhile(() => !Game.IsAddonActive("MateriaAttachDialog"), "AttachDialog");

        unsafe { Game.FireCallback("MateriaAttachDialog", [0, 0, 1], true); }

        await WaitWhile(() => Game.PlayerIsMelding, "MeldFinish", 10);

        if (overmeldSlot >= 0)
        {
            var expectedMat = foundItem.GetMateria(foundItem.NormalSlotCount + overmeldSlot);
            if (expectedMat != m)
                throw new MeldFailedException(foundItem, m);
        }
    }

    private async System.Threading.Tasks.Task EnsureSlotEmpty(ItemRef foundItem, int slotIndex)
    {
        if (foundItem.MateriaCount <= slotIndex)
            return;

        Status = $"Retrieving {foundItem.MateriaCount - slotIndex} materia from {foundItem}";
        log?.Report(Status);

        if (opts.DryRun)
            return;

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
    public class ItemNotFoundException(uint itemId, bool hq) : Exception
    {
        public unsafe ItemNotFoundException(ItemRef i) : this(i.Item.Value->ItemId, i.Item.Value->IsHighQuality()) { }

        public override string Message => $"No {ItemName(itemId)} (hq={hq}) in inventory.";
    }
    public class MateriaNotFoundException(Mat mat) : Exception
    {
        public override string Message => $"No {mat.Item.Name} in inventory.";
    }
    public class MeldFailedException(ItemRef i, Mat mat) : Exception
    {
        public override string Message => $"Ran out of materia while trying to attach {mat} to {i}.";
    }

    protected async System.Threading.Tasks.Task OpenAgent()
    {
        if (CancelToken.IsCancellationRequested)
            Error("task canceled by user");

        if (Game.IsAttachAgentActive())
            return;

        bool res;

        unsafe { res = ActionManager.Instance()->UseAction(ActionType.GeneralAction, 13); }

        ErrorIf(!res, "Unable to start melding - do you have it unlocked?");

        await WaitWhile(() => !Game.IsAttachAgentActive(), "OpenAgent");
    }

    protected async System.Threading.Tasks.Task SelectItem(ItemRef item)
    {
        var cat = GetCategory(item);
        ErrorIf(cat == AgentMateriaAttach.FilterCategory.None, $"Item {item} has no valid inventory category");

        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            if (agent->Category != cat)
                Game.AgentReceiveEvent(&AgentMateriaAttach.Instance()->AgentInterface, 0, [0, (int)cat]);
        }

        await WaitWhile(Game.AgentLoading, "WaitAgentLoad");

        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            var it = item.Item.Value;
            for (var i = 0; i < agent->ItemCount; i++)
            {
                if (it == agent->Data->ItemsSorted[i].Value->Item)
                {
                    Game.AgentReceiveEvent(&agent->AgentInterface, 0, [1, i, 1, 0]);
                    return;
                }
            }

            throw new ItemNotFoundException(item);
        }
    }

    protected async System.Threading.Tasks.Task SelectMateria(Mat m)
    {
        ErrorIf(Game.SelectedItem() < 0, "No item selected in agent");
        await WaitWhile(Game.AgentLoading, "WaitAgentLoad");

        var materiaItemId = m.Item.RowId;

        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            for (var i = 0; i < agent->MateriaCount; i++)
            {
                var invItem = agent->Data->MateriaSorted[i].Value->Item;
                if (invItem->ItemId == materiaItemId)
                {
                    Game.AgentReceiveEvent(&agent->AgentInterface, 0, [2, i, 1, 0]);
                    return;
                }
            }

            throw new MateriaNotFoundException(m);
        }
    }

    private AgentMateriaAttach.FilterCategory GetCategory(ItemRef item)
    {
        unsafe
        {
            return item.Item.Value->GetInventoryType() switch
            {
                InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4 => AgentMateriaAttach.FilterCategory.Inventory,
                InventoryType.ArmoryMainHand or InventoryType.ArmoryOffHand => AgentMateriaAttach.FilterCategory.ArmouryWeapon,
                InventoryType.ArmoryHead or InventoryType.ArmoryBody or InventoryType.ArmoryHands => AgentMateriaAttach.FilterCategory.ArmouryHeadBodyHands,
                InventoryType.ArmoryLegs or InventoryType.ArmoryFeets => AgentMateriaAttach.FilterCategory.ArmouryLegsFeet,
                InventoryType.ArmoryEar or InventoryType.ArmoryNeck => AgentMateriaAttach.FilterCategory.ArmouryNeckEars,
                InventoryType.ArmoryWrist or InventoryType.ArmoryRings => AgentMateriaAttach.FilterCategory.ArmouryWristRing,
                InventoryType.EquippedItems => AgentMateriaAttach.FilterCategory.Equipped,
                _ => AgentMateriaAttach.FilterCategory.None
            };
        }
    }

}
