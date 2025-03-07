using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CopeSeetheMeld;

public partial class Import(string input) : AutoTask
{
    private HttpClient client = new();
    private readonly JsonSerializerOptions jop = new() { IncludeFields = true };

    [GeneratedRegex(@"https?:\/\/etro\.gg\/gearset\/([^/]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternEtro();

    [GeneratedRegex(@"https?:\/\/xivgear\.app\/\?page=sl%7C([a-zA-Z0-9-]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternXIVG();

    [GeneratedRegex(@"https?:\/\/api\.xivgear\.app\/shortlink\/([a-zA-Z0-9-]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternXIVGRaw();

    protected override async Task Execute()
    {
        var m1 = PatternEtro().Match(input);
        if (m1.Success)
        {
            await ImportEtro(m1.Groups[1].Value);
            return;
        }

        var m2 = PatternXIVG().Match(input);
        if (m2.Success)
        {
            await ImportXIVG(m2.Groups[1].Value);
            return;
        }

        var m3 = PatternXIVGRaw().Match(input);
        if (m3.Success)
        {
            await ImportXIVG(m3.Groups[1].Value);
            return;
        }

        Error("Unrecognized input");
    }

    private async Task ImportEtro(string gearsetId)
    {
        Status = "Importing from Etro.gg";
        Log("Importing from etro");
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
        public uint id;
    }

    private async Task ImportXIVG(string shortcode)
    {
        Status = "Importing from XIVGear";

        var contents = await client.GetStringAsync($"https://api.xivgear.app/shortlink/{shortcode}");
        var xgs = JsonSerializer.Deserialize<XGSet>(contents, jop) ?? throw new Exception("Bad response from server");

        var gs = new Gearset(xgs.name);

        void mk(ItemType ty, string field)
        {
            if (!xgs.items.TryGetValue(field, out var item))
                return;

            var mats = new uint[5];
            for (var i = 0; i < Math.Min(5, item.materia.Count); i++)
                mats[i] = item.materia[i].id;

            gs[ty] = new ItemSlot(item.id, mats);
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
        mk(ItemType.RingL, "RingLeft");
        mk(ItemType.RingR, "RingRight");

        Plugin.Config.Gearsets.Add(xgs.name, gs);
    }
}
