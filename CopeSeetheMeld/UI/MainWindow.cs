using CopeSeetheMeld.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
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
        using (var cfg = ImRaii.TabItem("Settings"))
        {
            if (cfg)
                Plugin.Config.LastUsedOptions.Draw();
        }
    }

    private Source retrieveSources;

    private void DrawUtils()
    {
        ImGui.Text("Bulk retrieve");
        ImGui.Separator();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Sources: ");
        DrawButton("Equipped", Source.Equipped);
        DrawButton("Inventory", Source.Inventory);
        DrawButton("Armoury", Source.Armoury);
        using (ImRaii.Disabled(retrieveSources == default))
            if (ImGui.Button("Retrieve all"))
                auto.Start(new Retrieve(retrieveSources));
    }

    private void DrawButton(string label, Source flag)
    {
        var active = retrieveSources.HasFlag(flag);

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, 0xFF008080, active))
            if (ImGui.Button(label))
            {
                if (active)
                    retrieveSources &= ~flag;
                else
                    retrieveSources |= flag;
            }
    }
}
