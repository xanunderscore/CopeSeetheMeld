using System.Text.Json;

await ThingDoer.DoThing();

public class ThingDoer
{
    public class EtroGearset
    {
        public required Dictionary<string, Dictionary<string, uint>> materia;
        public uint? weapon;
        public uint? head;
        public uint? body;
        public uint? hands;
        public uint? legs;
        public uint? feet;
        public uint? offHand;
        public uint? ears;
        public uint? neck;
        public uint? wrists;
        public uint? fingerL;
        public uint? fingerR;
    }

    public static async Task DoThing()
    {
        var client = new HttpClient();
        var gearsetId = "01e208dc-6fd0-4a80-a26a-a7f770b19c2b";

        var opts = new JsonSerializerOptions { IncludeFields = true };

        var contents = await client.GetStringAsync($"https://etro.gg/api/gearsets/{gearsetId}");
        Console.WriteLine(contents);
        var gearset = JsonSerializer.Deserialize<EtroGearset>(contents, opts);
        var serialized = JsonSerializer.Serialize(gearset, opts);
        Console.WriteLine($"re-serialized: {serialized}");
    }
}
