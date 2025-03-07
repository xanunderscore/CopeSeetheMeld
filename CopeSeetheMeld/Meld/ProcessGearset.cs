using System.Threading.Tasks;

namespace CopeSeetheMeld;

public unsafe class ProcessGearset(Gearset goal) : AutoTask
{
    protected override async Task Execute()
    {
        foreach (var (itemSlot, item) in goal.Slots)
        {
            Plugin.Log.Debug($"Looking for {item.Id} for slot {itemSlot}");
        }
    }
}
