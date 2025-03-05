using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopeSeetheMeld;

public static class Meld
{
    public interface ITask { }
    public record struct TaskMeld(Gearsets.InventoryLocation Item, uint ItemId, uint MateriaId, int Slot) : ITask { }
    public record struct TaskRetrieve(Gearsets.InventoryLocation Item, uint ItemId, int Slot) : ITask { }

    public static readonly List<ITask> Tasks = [];
    public static readonly List<string> Messages = [];

    public static void Start(GearsetBase<Gearsets.ItemStatus> goal)
    {
        Tasks.Clear();
        Messages.Clear();

        foreach (var (itemSlot, item) in goal)
        {
            if (item.GearsetItem.Id == 0)
                continue;

            if (item.Source is not Gearsets.InventoryLocation loc)
            {
                Messages.Add($"No {Plugin.ItemName(item.GearsetItem.Id)} found in inventory, skipping");
                continue;
            }

            var materiaSlot = 0;
            foreach (var (want, have) in item.GearsetItem.Materia.Zip(item.Materia))
            {
                if (want == 0)
                    break;
                if (want != have)
                {
                    if (have != 0)
                        Tasks.Insert(0, new TaskRetrieve(loc, item.GearsetItem.Id, materiaSlot));
                    Tasks.Add(new TaskMeld(loc, item.GearsetItem.Id, want, materiaSlot));
                }
                materiaSlot++;
            }
        }

        foreach (var t in Tasks)
            Perform(t);

        foreach (var m in Messages)
            Plugin.Log.Warning(m);
    }

    private static void Perform(ITask t)
    {
        if (t is TaskMeld tm)
            DoMeld(tm);
        if (t is TaskRetrieve um)
            DoRetrieve(um);
    }

    private static unsafe int GetRegularInventoryItemCount(uint itemId)
    {
        var im = InventoryManager.Instance();
        return im->GetItemCountInContainer(itemId, InventoryType.Inventory1)
            + im->GetItemCountInContainer(itemId, InventoryType.Inventory2)
            + im->GetItemCountInContainer(itemId, InventoryType.Inventory3)
            + im->GetItemCountInContainer(itemId, InventoryType.Inventory4);
    }

    private static unsafe void DoMeld(TaskMeld t)
    {
        var item = InventoryManager.Instance()->GetInventorySlot(t.Item.InventoryType, t.Item.Location);
        if (item == null)
        {
            Messages.Add($"Item {Plugin.ItemName(t.ItemId)} has disappeared from inventory, continuing");
            return;
        }

        var lastSlot = item->Materia.IndexOf((ushort)0);

        if (t.Slot > lastSlot)
        {
            Messages.Add($"Cannot meld materia into slot {t.Slot} - item {Plugin.ItemName(t.ItemId)} has {lastSlot} filled, continuing");
            return;
        }

        if (GetRegularInventoryItemCount(t.MateriaId) == 0)
        {
            Messages.Add($"Not enough {Plugin.ItemName(t.MateriaId)} in inventory to meld {Plugin.ItemName(t.ItemId)}, continuing");
            return;
        }
    }

    private static void DoRetrieve(TaskRetrieve t)
    {
    }
}
