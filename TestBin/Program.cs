await ThingDoer.DoThing();

public class ThingDoer
{
    public static async Task DoThing()
    {
        string[] want = ["A", "A", "B", "D", "C"];

        string[] have = ["B", "A", "B", "A", "C"];

        var wantCnt = counts(want);

        for (var i = 0; i < have.Length; i++)
        {
            var w = have[i];
            if (wantCnt.TryGetValue(w, out var value))
            {
                wantCnt[w] = value - 1;
                if (wantCnt[w] == 0)
                    wantCnt.Remove(w);
            }
            else
            {
                Console.WriteLine($"retrieve at {i}");
                var remaining = wantCnt.SelectMany(k => Enumerable.Repeat(k.Key, k.Value)).ToList();
            }
        }
    }

    private static Dictionary<T, int> counts<T>(IEnumerable<T> items) where T : notnull => items.GroupBy(v => v).Select(v => (v.Key, v.Count())).ToDictionary();
}
