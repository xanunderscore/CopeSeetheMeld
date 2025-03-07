using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Threading.Tasks;

namespace CopeSeetheMeld.Meld;

public unsafe class RetrieveMulti(InventoryItem* Item, int FirstEmptySlot) : AutoTask
{
    protected override async Task Execute()
    {
        throw new NotImplementedException();
    }
}
