using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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
public class ItemSlot(uint id, uint[]? materia = null)
{
    public readonly uint Id = id;
    public readonly uint[] Materia = materia ?? new uint[5];
}

[Serializable]
public class Gearset(string name)
{
    public readonly string Name = name;
    public readonly ItemSlot[] Items = Enumerable.Repeat(new ItemSlot(0), 12).ToArray();

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
