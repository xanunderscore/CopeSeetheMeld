using CopeSeetheMeld.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace CopeSeetheMeld;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Hooking { get; private set; } = null!;

    private const string CommandName = "/cope";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("CSM");
    private MainWindow MainWindow { get; init; }
    private Interop Interop { get; init; }

    public Plugin()
    {
        Interop = new(Hooking);
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(this) { IsOpen = true };

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Meld"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        Configuration.Save();
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
        Interop.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();
    public static Item? Item(uint itemId) => DataManager.GetExcelSheet<Item>()?.GetRow(itemId);

    public static string ItemName(uint item) => item == 0 ? "<none>" : Item(item)?.Name.ToString() ?? "(unknown)";
}
