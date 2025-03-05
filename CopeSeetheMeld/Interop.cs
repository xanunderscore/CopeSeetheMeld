using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using System;

namespace CopeSeetheMeld;

public class Interop : IDisposable
{
    public unsafe delegate void ExecuteMeldDelegate(FFXIVClientStructs.FFXIV.Client.Game.InventoryType gearContainer, ushort gearSlot, FFXIVClientStructs.FFXIV.Client.Game.InventoryType materiaContainer, ushort materiaSlot, uint playerId, bool unk1);

    [Signature("E8 ?? ?? ?? ?? 48 8B 74 24 ?? B0 01 48 8B 5C 24 ?? 48 8B 6C 24 ?? 48 8B 7C 24 ??")]
    private Hook<ExecuteMeldDelegate> executeMeldHook = null!;

    public Interop(IGameInteropProvider hookProvider)
    {
        hookProvider.InitializeFromAttributes(this);
        executeMeldHook.Enable();
    }

    public void Dispose()
    {
        executeMeldHook.Dispose();
    }

    private unsafe void ExecuteMeldDetour(FFXIVClientStructs.FFXIV.Client.Game.InventoryType gearContainer, ushort gearSlot, FFXIVClientStructs.FFXIV.Client.Game.InventoryType materiaContainer, ushort materiaSlot, uint playerId, bool unk1)
    {
        Plugin.Log.Debug($"Executing meld: {gearContainer}, {gearSlot}, {materiaContainer}, {materiaSlot}, {playerId}, {unk1}");
    }
}
