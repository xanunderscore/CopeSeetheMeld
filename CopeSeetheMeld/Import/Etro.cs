using System.Collections.Generic;
using System.Text.Json;

namespace CopeSeetheMeld.Import;

public partial class Import
{
    private async System.Threading.Tasks.Task ImportEtro(string gearsetId)
    {
        var contents = await client.GetStringAsync($"https://etro.gg/api/gearsets/{gearsetId}");

        var egs = JsonSerializer.Deserialize<EtroGearset>(contents, jop) ?? throw new System.Exception("Bad response from server");

        var gs = Gearset.Create(egs.name);

        void mk(ItemType ty, uint? itemId, string keyExtra = "")
        {
            if (itemId == null)
                return;

            var row = Data.Item(itemId.Value);

            var slot = ItemSlot.Create(itemId.Value, row.CanBeHq);
            if (egs.materia.TryGetValue($"{itemId}{keyExtra}", out var materias))
                for (var i = 1; i <= 5; i++)
                    if (materias.TryGetValue(i.ToString(), out var mid))
                        slot.Materia[i - 1] = mid;

            gs[ty] = slot;
        }

        mk(ItemType.Weapon, egs.weapon);
        mk(ItemType.Head, egs.head);
        mk(ItemType.Body, egs.body);
        mk(ItemType.Hands, egs.hands);
        mk(ItemType.Legs, egs.legs);
        mk(ItemType.Feet, egs.feet);
        mk(ItemType.Offhand, egs.offHand);
        mk(ItemType.Ears, egs.ears);
        mk(ItemType.Neck, egs.neck);
        mk(ItemType.Wrists, egs.wrists);
        mk(ItemType.RingL, egs.fingerL, "L");
        mk(ItemType.RingR, egs.fingerR, "R");

        Plugin.Config.Gearsets.Add(egs.name, gs);
    }

    public record class EtroGearset
    {
        public required string name;
        public required uint? weapon;
        public required uint? head;
        public required uint? body;
        public required uint? hands;
        public required uint? legs;
        public required uint? feet;
        public required uint? offHand;
        public required uint? ears;
        public required uint? neck;
        public required uint? wrists;
        public required uint? fingerL;
        public required uint? fingerR;
        public required Dictionary<string, Dictionary<string, uint>> materia;
    }
}
