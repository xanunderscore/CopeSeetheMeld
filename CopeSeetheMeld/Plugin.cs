using CopeSeetheMeld.Windows;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using System.IO;

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

    public static Configuration Config { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("CSM");
    private MainWindow MainWindow { get; init; }
    private MeldWindow MeldWindow { get; init; }

    public Plugin(IDalamudPluginInterface dalamud, ICommandManager commandManager, ISigScanner sigScanner, IDataManager dataManager, IGameInteropProvider hooking)
    {
        InteropGenerator.Runtime.Resolver.GetInstance.Setup(version: File.ReadAllText("ffxivgame.ver"), cacheFile: new(dalamud.ConfigDirectory.FullName + "/cs.json"));
        FFXIVClientStructs.Interop.Generated.Addresses.Register();
        InteropGenerator.Runtime.Resolver.GetInstance.Resolve();
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new() { IsOpen = true };
        MeldWindow = new();

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(MeldWindow);

        CommandManager.AddHandler("/cope", new CommandInfo(OnCope) { HelpMessage = "Meld" });
        CommandManager.AddHandler("/cmeld", new CommandInfo(OnMeld) { HelpMessage = "Open manual meld window" });

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
        CommandManager.RemoveHandler("/cmeld");
    }

    private void OnCope(string command, string args) => ToggleMainUI();
    private void OnMeld(string command, string args) => MeldWindow.Toggle();

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

    public static ExcelSheet<T> LuminaSheet<T>() where T : struct, IExcelRow<T> => DataManager.Excel.GetSheet<T>();
    public static T LuminaRow<T>(uint id) where T : struct, IExcelRow<T> => LuminaSheet<T>().GetRow(id);
}

/*
public static class ItemExtensions
{
    public static Item? ItemRowMaybe(this uint itemId) => Plugin.DataManager.GetExcelSheet<Item>().TryGetRow(itemId, out var row) ? row : null;
    public static Item ItemRow(this uint itemId) => ItemRowMaybe(itemId)!.Value;
    public static string ItemName(this uint itemId) => ItemRow(itemId).Name.ToString() ?? "(unknown)";
}
*/
