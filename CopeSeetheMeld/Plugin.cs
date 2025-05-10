using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel;

namespace CopeSeetheMeld;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hooking { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;

    public static Configuration Config { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("CSM");
    private UI.MainWindow MainWindow { get; init; }

    public Plugin(IDalamudPluginInterface dalamud, ICommandManager commandManager, ISigScanner sigScanner, IDataManager dataManager, IGameInteropProvider hooking)
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Migrate();

        MainWindow = new() { IsOpen = Config.IsOpen };

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler("/cope", new CommandInfo(OnCope) { HelpMessage = "Open meld UI" });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        Config.Save();
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        CommandManager.RemoveHandler("/cope");
    }

    private void OnCope(string command, string args) => ToggleMainUI();

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

    public static ExcelSheet<T> LuminaSheet<T>() where T : struct, IExcelRow<T> => DataManager.Excel.GetSheet<T>();
    public static T LuminaRow<T>(uint id) where T : struct, IExcelRow<T> => LuminaSheet<T>().GetRow(id);
}
