using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace CopeSeetheMeld;

public static class Data
{
    public record class Mat(uint Id, ushort Grade)
    {
        public Materia Row => Plugin.LuminaRow<Materia>(Id);
        public Item Item => Row.Item[Grade].Value;

        public override string ToString() => $"{Row.BaseParam.Value.Name} +{Row.Value[Grade]}";
    }

    public record class ItemRef(Pointer<InventoryItem> Item)
    {
        public unsafe uint Slot => Item.Value->GetSlot();
        public unsafe int NormalSlotCount => Plugin.LuminaRow<Item>(Item.Value->ItemId).MateriaSlotCount;
        public unsafe int MateriaCount => Item.Value->GetMateriaCount();
        public unsafe bool IsHQ => Item.Value->IsHighQuality();
        public unsafe InventoryType Container => Item.Value->GetInventoryType();
        public static unsafe implicit operator InventoryItem*(ItemRef f) => f.Item.Value;

        public unsafe Mat GetMateria(int slot) => new(Item.Value->GetMateriaId((byte)slot), Item.Value->GetMateriaGrade((byte)slot));

        public override unsafe string ToString() => $"{ItemName(Item.Value->GetBaseItemId())} (in {Container}/{Slot})";
    }


    private static readonly Func<string, Item?> ItemByName = Memo.Memoize<string, Item?>((name) => name == "" ? null : Plugin.LuminaSheet<Item>().FirstOrNull(x => x.Name == name));

    public static Item? GetItemByName(string name) => ItemByName(name);

    private static readonly Func<uint, Mat?> MateriaByItemId = Memo.Memoize<uint, Mat?>(itemId =>
    {
        if (itemId == 0)
            return null;

        foreach (var mat in Plugin.LuminaSheet<Materia>())
        {
            var grade = mat.Item.ToList().FindIndex(y => y.RowId == itemId);
            if (grade >= 0)
                return new(mat.RowId, (ushort)grade);
        }

        return null;
    });

    public static Item Item(uint id) => Plugin.LuminaRow<Item>(id);
    public static string ItemName(uint id) => Item(id).Name.ToString();

    public static Mat? GetMateriaById(uint itemId) => MateriaByItemId(itemId);
    public static bool TryGetMateriaById(uint itemId, [NotNullWhen(true)] out Mat? mat)
    {
        mat = GetMateriaById(itemId);
        return mat != null;
    }

    private record struct ItemFilter(uint ItemLevel, byte Rarity);

    public static List<ItemSlot> GetMissingTools(Gearset gs)
    {
        var tools = ToolSet.None;
        ItemFilter? filter = null;

        var materias = new List<uint>[2]; // 0 = main hand, 1 = offhand

        foreach (var it in gs.Items)
        {
            var (thisFilter, thisFlag) = IdentifyItem(it.Id);
            tools |= thisFlag;
            if (thisFlag != ToolSet.None)
            {
                if (filter != null && filter != thisFilter)
                {
                    Plugin.Log.Debug($"gearset contains items of mixed rarity or level, skipping");
                    return [];
                }
                filter ??= thisFilter;

                var key = it.Type == ItemType.Weapon ? 0 : 1;
                materias[key] = it.Materia;
            }
            if (tools.IsDoH() && tools.IsDoL())
            {
                Plugin.Log.Debug($"gearset contains DoH and DoL tools, skipping");
                return [];
            }
        }

        var missingSet = tools.IsDoL() ? (ToolSet.DoL & ~tools)
            : tools.IsDoH() ? (ToolSet.DoH & ~tools)
            : ToolSet.None;

        if (missingSet == ToolSet.None || filter is not { } flt)
            return [];

        var results = new List<ItemSlot>();

        foreach (var missingSlot in Enum.GetValues<ToolSet>().Where(t => t != ToolSet.None && missingSet.HasFlag(t)))
        {
            var flag = (int)missingSlot;
            var classJobCategory = BitOperations.TrailingZeroCount(flag > (1 << 9) ? (flag >> 10) : flag);
            var itemUiCategory = 12 + (classJobCategory * 2);
            if (missingSlot.IsOffHand())
                itemUiCategory += 1;

            var wantMateria = materias[missingSlot.IsOffHand() ? 1 : 0];

            var candidateItems = Plugin.LuminaSheet<Item>().Where(i => i.ItemUICategory.RowId == itemUiCategory && i.LevelItem.RowId == flt.ItemLevel && i.Rarity == flt.Rarity).ToList();

            if (candidateItems.Count == 1)
            {
                var it = candidateItems[0];
                results.Add(new ItemSlot(it.RowId, Import.Import.GetItemEquipType(it), it.CanBeHq, wantMateria));
            }
            else if (candidateItems.Count == 0)
            {
                Plugin.Log.Warning($"unable to find item of type {missingSlot} at level {flt.ItemLevel} and rarity {flt.Rarity}");
                return [];
            }
            else
            {
                Plugin.Log.Warning($"multiple items found for type {missingSlot} at level {flt.ItemLevel} and rarity {flt.Rarity}, please pick one manually");
                return [];
            }
        }

        return results;
    }

    private static (ItemFilter, ToolSet) IdentifyItem(uint itemId)
    {
        var it = Item(itemId);
        var filter = new ItemFilter(it.LevelItem.RowId, it.Rarity);
        var classIndex = (int)it.ClassJobCategory.RowId - 9;
        var weaponIndex = (int)it.EquipSlotCategory.RowId - 1;
        if (classIndex >= 0 && weaponIndex >= 0 && classIndex < 10 && weaponIndex < 2)
        {
            return (filter, (ToolSet)(1 << (classIndex + (10 * weaponIndex))));
        }
        return (filter, ToolSet.None);
    }
}

[Flags]
public enum ToolSet
{
    None = 0,

    CRP1 = 1 << 0,
    BSM1 = 1 << 1,
    ARM1 = 1 << 2,
    GSM1 = 1 << 3,
    LTW1 = 1 << 4,
    WVR1 = 1 << 5,
    ALC1 = 1 << 6,
    CUL1 = 1 << 7,

    MIN1 = 1 << 8,
    BTN1 = 1 << 9,

    CRP2 = 1 << 10,
    BSM2 = 1 << 11,
    ARM2 = 1 << 12,
    GSM2 = 1 << 13,
    LTW2 = 1 << 14,
    WVR2 = 1 << 15,
    ALC2 = 1 << 16,
    CUL2 = 1 << 17,

    MIN2 = 1 << 18,
    BTN2 = 1 << 19,

    MainHand = 0x3FF,
    OffHand = 0x3FF << 10,
    DoH = 0xFF | (0xFF << 10),
    DoL = 0x300 | (0x300 << 10)
}

public static class ToolSetExtensions
{
    public static bool IsMainHand(this ToolSet ts) => (ts & ToolSet.MainHand) == ts;
    public static bool IsOffHand(this ToolSet ts) => (ts & ToolSet.OffHand) == ts;
    public static bool IsDoH(this ToolSet ts) => (ts & ToolSet.DoH) == ts;
    public static bool IsDoL(this ToolSet ts) => (ts & ToolSet.DoL) == ts;

    public static bool IsValid(this ToolSet ts) => ts.IsDoH() != ts.IsDoL();
}
