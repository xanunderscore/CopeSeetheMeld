using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace CopeSeetheMeld;

public class MeldOptions
{
    public enum SpecialMode : uint
    {
        None,
        MeldOnly,
        RetrieveOnly
    }

    public bool DryRun = true;
    public bool Overmeld = true;
    public bool StopOnMissingItem = false;
    public bool StopOnMissingMateria = false;
    public SpecialMode Mode = SpecialMode.None;

    public void Draw()
    {
        ImGui.Checkbox("Dry run", ref DryRun);
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.Text(FontAwesomeIcon.InfoCircle.ToIconString());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Don't do any melds - just output what changes would be made given current gear.");

        EnumCombo("Mode", ModeString, ref Mode);

        DrawStopFlag("If an item is missing:", ref StopOnMissingItem);
        using (ImRaii.Disabled(Mode == SpecialMode.RetrieveOnly))
        {
            DrawStopFlag("If materia is missing:", ref StopOnMissingMateria);
            ImGui.Checkbox("Do overmelds", ref Overmeld);
        }
    }

    private static void EnumCombo<T>(string label, Func<T, string> display, ref T v) where T : Enum
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{label}:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        using var comb = ImRaii.Combo($"###{label}", display(v));
        if (comb)
        {
            foreach (var raw in Enum.GetValues(typeof(T)))
            {
                var opt = (T)raw;
                if (ImGui.Selectable(display(opt), v.Equals(opt)))
                    v = opt;
            }
        }
    }

    private static string ModeString(SpecialMode m) => m switch
    {
        SpecialMode.MeldOnly => "Meld only, skip items where retrieval is needed",
        SpecialMode.RetrieveOnly => "Retrieve only, do not meld",
        _ => "Normal"
    };

    private static void DrawStopFlag(string cond, ref bool stopFlag)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text(cond);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        using var comb = ImRaii.Combo($"###{cond}", stopFlag ? "Stop melding" : "Skip to next item");
        if (comb)
        {
            if (ImGui.Selectable("Stop melding", stopFlag))
                stopFlag = true;
            if (ImGui.Selectable("Skip to next item", !stopFlag))
                stopFlag = false;
        }
    }
}

public class MeldLog
{
    public List<string> Actions = [];
    public bool Done;

    public void Report(string msg) => Actions.Add(msg);
    public void Finish() => Done = true;
}
