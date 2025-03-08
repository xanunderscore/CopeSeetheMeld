using Dalamud.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopeSeetheMeld;
public enum ItemType
{
    Weapon,
    Head,
    Body,
    Hands,
    Legs,
    Feet,
    Offhand,
    Ears,
    Neck,
    Wrists,
    RingL,
    RingR
}

public class ItemSlot(uint id, IEnumerable<uint> materia)
{
    internal ItemSlot() : this(0, []) { }

    public static ItemSlot Create(uint id) => new(id, [0, 0, 0, 0, 0]);

    public uint Id = id;
    public List<uint> Materia = materia.ToList();
}

public class Gearset(string name, IEnumerable<ItemSlot> items)
{
    internal Gearset() : this("", []) { }

    public string Name = name;
    public List<ItemSlot> Items = items.ToList();

    public static Gearset Create(string name) => new(name, Enumerable.Repeat(ItemSlot.Create(0), 12));

    public ItemSlot this[ItemType index]
    {
        get => Items[(int)index];
        set => Items[(int)index] = value;
    }

    [JsonIgnore]
    public IEnumerable<(ItemType, ItemSlot)> Slots
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

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public Dictionary<string, Gearset> Gearsets = [];
    public string? SelectedGearset = null;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
