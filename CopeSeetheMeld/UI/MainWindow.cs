using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace CopeSeetheMeld.UI;

public partial class MainWindow : Window, IDisposable
{
    private readonly Automation auto = new();

    private readonly MeldUI meld;

    public MainWindow()
        : base("CSM", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        meld = new(auto);
    }

    public void Dispose()
    {
        meld.Dispose();
        auto.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void OnOpen()
    {
        Plugin.Config.IsOpen = true;
        base.OnOpen();
    }

    public override void OnClose()
    {
        Plugin.Config.IsOpen = false;
        base.OnClose();
    }

    public override void Draw()
    {
        using (ImRaii.Disabled(!auto.Running))
            if (ImGui.Button("Stop current task"))
                auto.Stop();
        ImGui.SameLine();
        ImGui.TextUnformatted($"Status: {auto.CurrentTask?.Status ?? "idle"}");

        using var tb = ImRaii.TabBar("###maintabs");
        using (var gs = ImRaii.TabItem("Gearsets"))
        {
            if (gs)
                meld.Draw();
        }
        using (var utils = ImRaii.TabItem("Utilities"))
        {
            if (utils)
                DrawUtils();
        }
    }

    [Flags]
    private enum RetrieveCategory
    {
        None = 0,
        Equipped = 1,
        Inventory = 2,
        Armoury = 4
    }

    private RetrieveCategory retrieveCategory;

    private void DrawUtils()
    {
        ImGui.Text("Bulk retrieve");
        ImGui.Separator();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Sources: ");
        DrawButton("Equipped", RetrieveCategory.Equipped);
        DrawButton("Inventory", RetrieveCategory.Inventory);
        DrawButton("Armoury", RetrieveCategory.Armoury);
        if (ImGui.Button("Retrieve all"))
            Plugin.Log.Debug("retrieve");
    }

    private void DrawButton(string label, RetrieveCategory flag)
    {
        var active = retrieveCategory.HasFlag(flag);

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, 0xFF008080, active))
            if (ImGui.Button(label))
            {
                if (active)
                    retrieveCategory &= ~flag;
                else
                    retrieveCategory |= flag;
            }
    }
}
