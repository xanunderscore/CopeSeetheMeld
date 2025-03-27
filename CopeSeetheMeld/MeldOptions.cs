using CopeSeetheMeld.UI;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Reflection;
using static CopeSeetheMeld.Data;

namespace CopeSeetheMeld;

public class MeldOptions
{
    public enum SpecialMode : uint
    {
        [Display("Normal")]
        None,
        [Display("Meld only - skip items that need retrieval")]
        MeldOnly,
        [Display("Retrieve only, don't meld")]
        RetrieveOnly
    }

    public enum StopBehavior : uint
    {
        [Display("Skip to next item")]
        Skip,
        [Display("Stop melding")]
        Stop
    }

    public bool DryRun = true;
    public bool Overmeld = true;
    public int MeldConfidence = 50;
    public StopBehavior StopOnMissingItem = StopBehavior.Skip;
    public StopBehavior StopOnMissingMateria = StopBehavior.Skip;
    public SpecialMode Mode = SpecialMode.None;

    public void Draw()
    {
        ImGui.Checkbox("Dry run", ref DryRun);
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.Text(FontAwesomeIcon.InfoCircle.ToIconString());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Don't do any melds - just output what changes would be made given current gear.");

        EnumCombo("Mode", ref Mode);

        EnumCombo("If an item is missing", ref StopOnMissingItem);
        using (ImRaii.Disabled(Mode == SpecialMode.RetrieveOnly))
        {
            EnumCombo("If materia is missing", ref StopOnMissingMateria);
            ImGui.Checkbox("Do overmelds", ref Overmeld);
        }
        using (ImRaii.Disabled(!Overmeld))
        {
            ImGui.SetNextItemWidth(200);
            ImGui.DragInt("Meld confidence", ref MeldConfidence, 0.25f, 50, 99, "%d%%");
        }
    }

    private static void EnumCombo<T>(string label, ref T v) where T : Enum
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{label}:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        using var comb = ImRaii.Combo($"###{label}", EnumString(v));
        if (comb)
        {
            foreach (var raw in Enum.GetValues(typeof(T)))
            {
                var opt = (T)raw;
                if (ImGui.Selectable(EnumString(opt), v.Equals(opt)))
                    v = opt;
            }
        }
    }

    public static string EnumString(Enum v)
    {
        var name = v.ToString();
        return v.GetType().GetField(name)?.GetCustomAttribute<DisplayAttribute>()?.Label ?? name;
    }
}

public class MeldLog
{
    public readonly List<string> Actions = [];
    public readonly Dictionary<Mat, int> MateriaUsed = [];
    public bool Done;

    public void Report(string msg) => Actions.Add(msg);
    public void ReportError(Exception ex) => Actions.Add(ex.Message);
    public void UseMateria(Mat materiaId, int count = 1)
    {
        var c = MateriaUsed.TryGetValue(materiaId, out var v) ? v : 0;
        MateriaUsed[materiaId] = c + count;
    }
    public void Finish()
    {
        Actions.Add("All done!");
        Done = true;
    }
}
