using System.Text.Json;
using System.Text.Json.Serialization;

await ThingDoer.DoThing();

class SerializableThing(string foo)
{
    public string Foo = foo;

    [JsonIgnore]
    public string Bar => "This should not be serialized";
}

public class ThingDoer
{
    public static async Task DoThing()
    {
        Console.WriteLine(JsonSerializer.Serialize(new SerializableThing("foo"), new JsonSerializerOptions { IncludeFields = true }));
    }
}
