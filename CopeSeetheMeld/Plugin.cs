using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Lumina.Excel.Sheets;
using SamplePlugin.Windows;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Hooking { get; private set; } = null!;

    private unsafe delegate byte TestFunctionDelegate(long a1, long a2, long a3, byte a4);

    [Signature("E8 ?? ?? ?? ?? 84 C0 74 40 48 8B CB 48 8B 5C 24 ??")]
    private Hook<TestFunctionDelegate> testFunctionHook = null!;

    private const string CommandName = "/cope";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("CSM");
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Hooking.InitializeFromAttributes(this);
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

        testFunctionHook.Enable();
    }

    public void Dispose()
    {
        Configuration.Save();
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
        testFunctionHook.Dispose();
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

    private unsafe byte TestFunctionDetour(long a1, long a2, long a3, byte a4)
    {
        Log.Debug($"{a1}, {a2}, {a3}, {a4}");
        return testFunctionHook.Original(a1, a2, a3, a4);
    }
}
