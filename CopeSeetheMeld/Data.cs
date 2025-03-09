using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
        public unsafe int SlotCount => Plugin.LuminaRow<Item>(Item.Value->ItemId).MateriaSlotCount;
        public unsafe int MateriaCount => Item.Value->GetMateriaCount();
        public unsafe InventoryType Container => Item.Value->GetInventoryType();
        public static unsafe implicit operator InventoryItem*(ItemRef f) => f.Item.Value;

        public override unsafe string ToString() => $"{Container}/{Slot}";
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
}
