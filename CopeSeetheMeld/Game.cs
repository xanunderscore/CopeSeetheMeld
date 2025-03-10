using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace CopeSeetheMeld;

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
