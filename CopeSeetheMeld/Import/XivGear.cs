using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CopeSeetheMeld.Import;

public partial class Import
{
    public record class XGSetCollection
    {
        public required string name;
        public required List<XGSet> sets;
    }

    public record class XGSet
    {
        public required string name;
        public required Dictionary<string, XGItem> items;
    }

    public record class XGItem
    {
        public uint id;
        public required List<XGId> materia;
    }

    public record class XGId
    {
        public int id;
    }

    private async System.Threading.Tasks.Task ImportXIVG(string shortcode, string gearIndex)
    {
        var ix = gearIndex.Length == 0 ? -1 : int.Parse(gearIndex);

        var contents = await client.GetStringAsync($"https://api.xivgear.app/shortlink/{shortcode}");
        XGSet xgs;
        if (ix >= 0)
        {
            var set = JsonSerializer.Deserialize<XGSetCollection>(contents, jop) ?? throw new Exception("Bad response from server");
            xgs = set.sets[ix];
        }
        else
        {
            // TODO even for single gearsets, api can return janky results sometimes and cause this to fail, e.g. https://xivgear.app/?page=sl%7Cf4dadaec-dedf-48d9-9936-caaa64033a30
            xgs = JsonSerializer.Deserialize<XGSet>(contents, jop) ?? throw new Exception("Bad response from server");
        }

        var gs = new Gearset(xgs.name);

        void mk(ItemType ty, string field)
        {
            if (!xgs.items.TryGetValue(field, out var item))
                return;

            var row = Data.Item(item.id);

            var slot = new ItemSlot(item.id, ty, row.CanBeHq);
            for (var i = 0; i < Math.Min(5, item.materia.Count); i++)
                if (item.materia[i].id >= 0)
                    slot.Materia[i] = (uint)item.materia[i].id;

            gs.Items.Add(slot);
        }

        mk(ItemType.Weapon, "Weapon");
        mk(ItemType.Head, "Head");
        mk(ItemType.Body, "Body");
        mk(ItemType.Hands, "Hand");
        mk(ItemType.Legs, "Legs");
        mk(ItemType.Feet, "Feet");
        mk(ItemType.Offhand, "OffHand");
        mk(ItemType.Ears, "Ears");
        mk(ItemType.Neck, "Neck");
        mk(ItemType.Wrists, "Wrist");
        mk(ItemType.Ring, "RingLeft");
        mk(ItemType.Ring, "RingRight");

        Plugin.Config.GearsetList.Add(gs);
    }
}
