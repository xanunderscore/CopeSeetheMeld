using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System;
using System.Threading.Tasks;
using static CopeSeetheMeld.Data;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace CopeSeetheMeld;

public abstract class AutoCommon : AutoTask
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
        public override string Message => $"Ran out of materia while trying to attach {mat} to {i}.";
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
            if (agent->ActiveFilter != cat)
                Game.AgentReceiveEvent(&AgentMateriaAttach.Instance()->AgentInterface, 0, [0, (int)cat]);
        }

        await WaitWhile(Game.AgentLoading, "WaitAgentLoad");

        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            var it = item.Item.Value;
            for (var i = 0; i < agent->ItemCount; i++)
            {
                if (it == *agent->Context->Items[i])
                {
                    Game.AgentReceiveEvent(&agent->AgentInterface, 0, [1, i, 1, 0]);
                    return;
                }
            }

            throw new ItemNotFoundException(item);
        }
    }

    protected async Task SelectMateria(Mat m)
    {
        ErrorIf(Game.SelectedItem() < 0, "No item selected in agent");
        await WaitWhile(Game.AgentLoading, "WaitAgentLoad");

        var materiaItemId = m.Item.RowId;

        unsafe
        {
            var agent = AgentMateriaAttach.Instance();
            for (var i = 0; i < agent->MateriaCount; i++)
            {
                var invItem = *agent->Context->Materia[i];
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
                _ => AgentMateriaAttach.FilterCategory.None
            };
        }
    }
}

internal static unsafe class Game
{
    public static bool IsAttachAgentActive() => AgentMateriaAttach.Instance()->IsAgentActive();
    public static bool AgentLoading() => AgentMateriaAttach.Instance()->StateVar != 0;
    public static AgentMateriaAttach.FilterCategory ActiveFilter() => AgentMateriaAttach.Instance()->ActiveFilter;
    public static int SelectedItem() => AgentMateriaAttach.Instance()->SelectedItem;

    public static AtkValue AgentReceiveEvent(AgentInterface* agent, ulong eventKind, int[] args) => WithArgs(args, (ptr, len) =>
        {
            var ret = new AtkValue();
            agent->ReceiveEvent(&ret, ptr, len, eventKind);
            return ret;
        });

    public static void FireCallback(AtkUnitBase* addon, int[] args, bool close = false)
        => WithArgs(args, (ptr, len) => addon->FireCallback(len, ptr.Value, close));

    public static void FireCallback(string addonName, int[] args, bool close = false)
    {
        var addon = GetActiveAddon(addonName);
        if (addon == null)
            throw new Exception($"Addon {addonName} was expected to be active, but isn't");

        FireCallback(addon, args, close);
    }

    public static T WithArgs<T>(int[] args, Func<Pointer<AtkValue>, uint, T> callback)
    {
        var values = stackalloc AtkValue[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            values[i].Type = ValueType.Int;
            values[i].Int = args[i];
        }
        return callback(values, (uint)args.Length);
    }

    public static void WithArgs(int[] args, Action<Pointer<AtkValue>, uint> callback) => WithArgs<object>(args, (a, b) => { callback(a, b); return null!; });

    public static bool IsAddonActive(string name) => GetActiveAddon(name) != null;

    private static AtkUnitBase* GetActiveAddon(string name)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible && addon->IsReady ? addon : null;
    }

    public static bool PlayerIsBusy => !Plugin.Condition[ConditionFlag.NormalConditions] || Plugin.Condition[ConditionFlag.Occupied39] || Plugin.Condition[ConditionFlag.Jumping];

    public static bool PlayerIsMelding => Plugin.Condition[ConditionFlag.MeldingMateria];
    public static bool PlayerIsRetrieving => Plugin.Condition[ConditionFlag.Occupied39];
}
