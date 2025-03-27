using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CopeSeetheMeld.Import;

public partial class Import(string input) : AutoTask
{
    private readonly HttpClient client = new();
    private readonly JsonSerializerOptions jop = new() { IncludeFields = true };

    [GeneratedRegex(@"https?:\/\/etro\.gg\/gearset\/([^/]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternEtro();

    [GeneratedRegex(@"(?:https?:\/\/xivgear\.app\/\?page=sl\||https?:\/\/api\.xivgear\.app\/shortlink\/)([a-zA-Z0-9-]+)(?:&(?:selectedIndex|onlySetIndex)=(\d+))?", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternXIVG();

    protected override async Task Execute()
    {
        // teamcraft export is just markdown
        if (input.StartsWith("**"))
        {
            Status = "Importing from TC";
            ImportTeamcraft(input);
            return;
        }

        input = Uri.UnescapeDataString(input);

        var m1 = PatternEtro().Match(input);
        if (m1.Success)
        {
            Status = "Importing from Etro";
            await ImportEtro(m1.Groups[1].Value);
            return;
        }

        var m2 = PatternXIVG().Match(input);
        if (m2.Success)
        {
            Status = "Importing from xivgear";
            await ImportXIVG(m2.Groups[1].Value, m2.Groups[2].Value);
            return;
        }

        Error("Unrecognized input");
    }
}
