using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CopeSeetheMeld.Data;

namespace CopeSeetheMeld.Tasks;

[Flags]
public enum Source
{
    None = 0,
    Equipped = 1,
    Inventory = 2,
    Armoury = 4
}

public class Retrieve(Source sources) : MeldCommon
{
    protected override async Task Execute()
    {
        List<ItemRef> items = [];

        if (sources.HasFlag(Source.Equipped))
            items.AddRange(CollectItems(InventoryType.EquippedItems));

        if (sources.HasFlag(Source.Inventory))
            items.AddRange(CollectItems(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4));

        if (sources.HasFlag(Source.Armoury))
            items.AddRange(CollectItems(InventoryType.ArmoryMainHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryOffHand, InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings));

        foreach (var it in items)
            await RetrieveOne(it);
    }

    private unsafe List<ItemRef> CollectItems(params InventoryType[] inventories)
    {
        List<ItemRef> result = [];

        var im = InventoryManager.Instance();
        foreach (var ty in inventories)
        {
            var container = im->GetInventoryContainer(ty);
            for (var i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item == null)
                    continue;

                var ir = new ItemRef(item);
                if (ir.NormalSlotCount == 0)
                    continue;

                result.Add(ir);
            }
        }

        return result;
    }

    private async Task RetrieveOne(ItemRef item)
    {
        if (item.MateriaCount <= 0)
            return;

        Status = $"Retrieving from {item}";

        while (item.MateriaCount > 0)
        {
            await OpenAgent();
            await SelectItem(item);

            unsafe { Game.AgentReceiveEvent(&AgentMateriaAttach.Instance()->AgentInterface, 4, [0, 1, 0, 0, 0]); }

            await WaitWhile(() => !Game.IsAddonActive("MateriaRetrieveDialog"), "RetrieveDialog");

            unsafe { Game.FireCallback("MateriaRetrieveDialog", [0], true); }

            await WaitWhile(() => Game.PlayerIsRetrieving, "RetrieveFinish", 10);
        }
    }
}
