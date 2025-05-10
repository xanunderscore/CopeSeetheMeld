using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace CopeSeetheMeld;

public class ItemSlotOld(uint id, bool hq, IEnumerable<uint> materia)
{
    internal ItemSlotOld() : this(0, false, []) { }

    public static ItemSlotOld Create(uint id, bool hq = false) => new(id, hq, [0, 0, 0, 0, 0]);

    public uint Id = id;
    public bool HighQuality = hq;
    public List<uint> Materia = materia.ToList();

    public override string ToString() => $"ItemSlot {{ Id = {Id}, HQ = {HighQuality}, Materia = [{string.Join(", ", Materia.TakeWhile(m => m > 0))}] }}";
}

public class GearsetOld(string name, IEnumerable<ItemSlotOld> items)
{
    internal GearsetOld() : this("", []) { }

    public string Name = name;
    public List<ItemSlotOld> Items = items.ToList();

    public static GearsetOld Create(string name) => new(name, Enumerable.Repeat(ItemSlotOld.Create(0), 12));

    public ItemSlotOld this[ItemType index]
    {
        get => Items[(int)index];
        set => Items[(int)index] = value;
    }

    [JsonIgnore]
    public IEnumerable<(ItemType, ItemSlotOld)> Slots
    {
        get
        {
            for (var i = 0; i < 12; i++)
            {
                var it = Items[i];
                if (it.Id > 0)
                    yield return ((ItemType)i, it);
            }
        }
    }
}
