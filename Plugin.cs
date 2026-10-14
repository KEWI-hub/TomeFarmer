using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace TomeFarmer;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/tomefarm";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly FarmController farm;
    private readonly WindowSystem windows = new("TomeFarmer");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var autoDuty = new AutoDutyIpc();
        farm = new FarmController(config, autoDuty);
        mainWindow = new MainWindow(config, farm, autoDuty);
        windows.AddWindow(mainWindow);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open TomeFarmer. Subcommands: start | stop",
        });

        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "start": farm.Start(); break;
            case "stop": farm.Stop(); break;
            default: mainWindow.Toggle(); break;
        }
    }

    public void Dispose()
    {
        farm.Stop("Plugin unloaded");
        farm.Dispose();
        CommandManager.RemoveHandler(Command);
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        windows.RemoveAllWindows();
    }
}
