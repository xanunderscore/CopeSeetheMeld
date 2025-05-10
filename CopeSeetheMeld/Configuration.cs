using Dalamud.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CopeSeetheMeld;

public enum ItemType
{
    Invalid = -1,
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
    Ring,
}

public class ItemSlot
{
    public uint Id;
    public bool HighQuality;
    public ItemType Type;
    public List<uint> Materia;

    [JsonConstructor]
    [SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "thanks visual studio!")]
    public ItemSlot(uint id, ItemType type, bool hq, List<uint> materia)
    {
        Id = id;
        Type = type;
        HighQuality = hq;
        Materia = materia;
    }
    public ItemSlot(uint id, ItemType type, bool hq) : this(id, type, hq, [0, 0, 0, 0, 0]) { }

    public override string ToString() => $"ItemSlot {{ Id = {Id}, HQ = {HighQuality}, Materia = [{string.Join(", ", Materia.TakeWhile(m => m > 0))}] }}";
}

public class Gearset
{
    public string Name;
    public List<ItemSlot> Items;

    [JsonConstructor]
    [SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "thanks visual studio!")]
    public Gearset(string name, List<ItemSlot> items)
    {
        Name = name;
        Items = items;
    }
    public Gearset(string name) : this(name, []) { }

    public IEnumerable<(ItemType Type, ItemSlot Item)> EnumerateItems() => Items.Select(i => (i.Type, i));

    internal void Sort()
    {
        Items.Sort((a, b) => SortOrder(a.Type).CompareTo(SortOrder(b.Type)));
    }

    public static int SortOrder(ItemType t) => t switch
    {
        ItemType.Weapon => 0,
        ItemType.Offhand => 1,
        ItemType.Head => 2,
        ItemType.Body => 3,
        ItemType.Hands => 4,
        ItemType.Legs => 5,
        ItemType.Feet => 6,
        ItemType.Ears => 7,
        ItemType.Neck => 8,
        ItemType.Wrists => 9,
        ItemType.Ring => 10,
        _ => int.MaxValue
    };
}

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsOpen = false;
    public MeldOptions LastUsedOptions = new();

    [Obsolete("Use GearsetList instead")]
    public Dictionary<string, GearsetOld> Gearsets = [];
    [Obsolete("Use SelectedIndex")]
    public string? SelectedGearset = null;

    public List<Gearset> GearsetList = [];
    public int SelectedIndex = -1;

    public Gearset? GetSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < GearsetList.Count)
            return GearsetList[SelectedIndex];

        return null;
    }

    public void Rename(int index, string name)
    {
        var existing = GearsetList.FindIndex(g => g.Name == name);
        if (existing >= 0 && existing != index)
        {
            Plugin.Log.Warning($"Rename will cause name collision between {index} and {existing}, doing nothing");
            return;
        }
        GearsetList[index].Name = name;
    }

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        foreach (var g in GearsetList)
            g.Sort();
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    internal void Migrate()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        foreach (var (name, gs) in Gearsets)
        {
            GearsetList.Add(new(name, [.. gs.Slots.Select(s => new ItemSlot(s.Item2.Id, s.Item1, s.Item2.HighQuality, s.Item2.Materia))]));
        }
        Gearsets.Clear();
        foreach (var g in GearsetList)
        {
            foreach (var i in g.Items)
            {
                // removed separate right/left ring types
                i.Type = (int)i.Type == 11 ? ItemType.Ring : i.Type;
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
