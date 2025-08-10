using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Threading.Tasks;
using static CopeSeetheMeld.Data;

namespace CopeSeetheMeld.Tasks;

public abstract class MeldCommon : AutoTask
{
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
        public string ItemDescription { get; init; } = i.ToString();
        public override string Message => $"Ran out of materia while trying to attach {mat} to {ItemDescription}.";
    }

    protected async Task OpenAgent()
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

    protected async Task SelectItem(ItemRef item)
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

    protected AgentMateriaAttach.FilterCategory GetCategory(ItemRef item)
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
