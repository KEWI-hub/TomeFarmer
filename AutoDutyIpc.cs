using System;
using Dalamud.Plugin.Ipc;

namespace TomeFarmer;

/// <summary>
/// Thin wrapper around AutoDuty's EzIPC endpoints (prefix "AutoDuty.").
/// Signatures verified against AutoDuty 0.0.0.377.
/// </summary>
public sealed class AutoDutyIpc
{
    private readonly ICallGateSubscriber<uint, int, bool, object> run;
    private readonly ICallGateSubscriber<object> stop;
    private readonly ICallGateSubscriber<bool> isStopped;
    private readonly ICallGateSubscriber<bool> isLooping;
    private readonly ICallGateSubscriber<bool> isNavigating;
    private readonly ICallGateSubscriber<uint, bool> contentHasPath;

    public AutoDutyIpc()
    {
        var pi = Plugin.PluginInterface;
        run = pi.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        stop = pi.GetIpcSubscriber<object>("AutoDuty.Stop");
        isStopped = pi.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        isLooping = pi.GetIpcSubscriber<bool>("AutoDuty.IsLooping");
        isNavigating = pi.GetIpcSubscriber<bool>("AutoDuty.IsNavigating");
        contentHasPath = pi.GetIpcSubscriber<uint, bool>("AutoDuty.ContentHasPath");
    }

    public bool IsAvailable
    {
        get
        {
            try { isStopped.InvokeFunc(); return true; }
            catch { return false; }
        }
    }

    public void Run(uint territoryType, int loops, bool bareMode) => run.InvokeAction(territoryType, loops, bareMode);
    public void Stop() => stop.InvokeAction();
    public bool IsStopped() => Safe(isStopped.InvokeFunc, true);
    public bool IsLooping() => Safe(isLooping.InvokeFunc, false);
    public bool IsNavigating() => Safe(isNavigating.InvokeFunc, false);
    public bool ContentHasPath(uint territoryType) => Safe(() => contentHasPath.InvokeFunc(territoryType), false);

    private static T Safe<T>(Func<T> f, T fallback)
    {
        try { return f(); }
        catch { return fallback; }
    }
}
