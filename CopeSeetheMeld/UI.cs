using ImGuiNET;
using Lumina.Excel.Sheets;
using System;

namespace CopeSeetheMeld;

public static class UI
{
    public static void Draw(Item item, bool hq = false)
    {
        var ic = Plugin.TextureProvider.GetFromGameIcon(new(item.Icon, hq)).GetWrapOrEmpty();
        ImGui.Image(ic.ImGuiHandle, new(64, 64));
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class DisplayAttribute(string label) : Attribute
{
    public string Label => label;
}
