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

[Serializable]
public record struct ItemSlot(uint Id, uint Materia1 = 0, uint Materia2 = 0, uint Materia3 = 0, uint Materia4 = 0, uint Materia5 = 0)
{

    [JsonIgnore]
    public readonly IEnumerable<uint> Materia => [Materia1, Materia2, Materia3, Materia4, Materia5];
}

[Serializable]
public class Gearset : GearsetBase<ItemSlot>;

public class GearsetBase<TItem> where TItem : struct
{
    public required string Name;
    [JsonProperty]
    private Dictionary<ItemType, TItem> items { get; init; } = [];

    public TItem this[ItemType ty]
    {
        get { return items.GetValueOrDefault(ty); }
        set { items[ty] = value; }
    }

    public Dictionary<ItemType, TItem>.Enumerator GetEnumerator() => items.GetEnumerator();
    public GearsetBase<TItem2> Map<TItem2>(Func<TItem, TItem2> f) where TItem2 : struct =>
        new() { Name = Name, items = items.ToDictionary(k => k.Key, k => f(k.Value)) };
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
