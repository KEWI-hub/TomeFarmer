using System;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace TomeFarmer;

public enum FarmState
{
    Idle,
    WaitingForStart,
    Running,
}

/// <summary>
/// Hands AutoDuty one continuous loop (no idle gap between runs) and watches duty enter/exit edges
/// to measure each run. Stops AutoDuty once a stop condition is met.
/// </summary>
public sealed class FarmController : IDisposable
{
    public const string MathematicsName = "Allagan Tomestone of Mathematics";
    public const int HoldingCap = 2000;
    private const int UnlimitedLoops = 9999;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    private readonly Configuration config;
    private readonly AutoDutyIpc autoDuty;

    private DateTime stateSince = DateTime.UtcNow;
    private DateTime openWorldSince = DateTime.MinValue;
    private DateTime cycleStart;
    private DateTime dutyEnteredAt;
    private int countAtEntry;
    private bool wasInDuty;
    private bool pendingMeasure;
    private int zeroGainStreak;

    public FarmState State { get; private set; } = FarmState.Idle;
    public string StatusText { get; private set; } = "Idle";
    public int RunsCompleted { get; private set; }
    public int SessionGained { get; private set; }
    public int LastRunGain { get; private set; }
    public TimeSpan LastRunTime { get; private set; }
    public DateTime? SessionStart { get; private set; }

    public uint ItemId { get; private set; }
    public string ItemDisplayName { get; private set; } = MathematicsName;

    public FarmController(Configuration config, AutoDutyIpc autoDuty)
    {
        this.config = config;
        this.autoDuty = autoDuty;
        ResolveItem();
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose() => Plugin.Framework.Update -= OnUpdate;

    public bool IsRunning => State != FarmState.Idle;

    public void ResolveItem()
    {
        if (config.ItemIdOverride != 0)
        {
            ItemId = config.ItemIdOverride;
        }
        else
        {
            // Look up by English name so it works on any client language.
            var en = Plugin.DataManager.GetExcelSheet<Item>(ClientLanguage.English);
            ItemId = en.FirstOrDefault(i => i.Name.ExtractText().Equals(MathematicsName, StringComparison.OrdinalIgnoreCase)).RowId;
        }

        var local = Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault(ItemId);
        ItemDisplayName = local is { } row && ItemId != 0 ? row.Name.ExtractText() : "(item not found)";
    }

    public unsafe int GetTomestoneCount()
    {
        if (ItemId == 0) return 0;
        var im = InventoryManager.Instance();
        return im == null ? 0 : im->GetInventoryItemCount(ItemId);
    }

    public void Start()
    {
        if (IsRunning) return;
        if (!autoDuty.IsAvailable) { Print("AutoDuty is not loaded."); return; }
        if (ItemId == 0) { Print("Could not find the Mathematics tomestone item id. Set it manually in settings."); return; }
        if (config.TerritoryType == 0) { Print("Pick a duty first."); return; }
        if (!autoDuty.ContentHasPath(config.TerritoryType)) { Print("AutoDuty has no path for the selected duty."); return; }
        if (!InOpenWorld()) { Print("Start from outside a duty."); return; }

        RunsCompleted = 0;
        SessionGained = 0;
        LastRunGain = 0;
        LastRunTime = TimeSpan.Zero;
        zeroGainStreak = 0;
        wasInDuty = false;
        pendingMeasure = false;
        SessionStart = cycleStart = DateTime.UtcNow;

        if (CheckStopConditions(out var reason)) { Print($"Not starting: {reason}"); return; }

        var loops = config.MaxRuns > 0 ? config.MaxRuns : UnlimitedLoops;
        autoDuty.Run(config.TerritoryType, loops, config.BareMode);
        Print($"Farming {ItemDisplayName}. Current: {GetTomestoneCount()}, target: {config.TargetCount}.");
        SetState(FarmState.WaitingForStart, "Waiting for AutoDuty to start");
    }

    public void Stop(string reason = "Stopped by user")
    {
        if (!IsRunning) return;
        if (!autoDuty.IsStopped()) autoDuty.Stop();
        SetState(FarmState.Idle, reason);
        Print($"{reason}. Runs: {RunsCompleted}, gained: {SessionGained}.");
    }

    private void OnUpdate(IFramework _)
    {
        if (State == FarmState.Idle) return;

        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "TomeFarmer tick failed");
            Stop($"Error: {ex.Message}");
        }
    }

    private void Tick()
    {
        var inDuty = Plugin.Condition[ConditionFlag.BoundByDuty] || Plugin.Condition[ConditionFlag.BoundByDuty56];

        if (State == FarmState.WaitingForStart)
        {
            if (!autoDuty.IsStopped() || inDuty) SetState(FarmState.Running, "Running");
            else if (DateTime.UtcNow - stateSince > StartTimeout) Stop("AutoDuty did not start within 5 minutes");
            return;
        }

        // Duty entry edge: snapshot count before rewards are granted.
        if (inDuty && !wasInDuty)
        {
            dutyEnteredAt = DateTime.UtcNow;
            countAtEntry = GetTomestoneCount();
            StatusText = $"Run #{RunsCompleted + 1}: in duty";
        }
        // Duty exit edge: measure once we're settled in the open world.
        if (!inDuty && wasInDuty)
        {
            pendingMeasure = true;
            StatusText = $"Run #{RunsCompleted + 1}: leaving duty";
        }
        wasInDuty = inDuty;

        if (InOpenWorld())
        {
            if (openWorldSince == DateTime.MinValue) openWorldSince = DateTime.UtcNow;
        }
        else
        {
            openWorldSince = DateTime.MinValue;
        }
        var settled = openWorldSince != DateTime.MinValue && DateTime.UtcNow - openWorldSince >= SettleTime;

        if (pendingMeasure && settled)
        {
            pendingMeasure = false;
            RecordRun();
            if (CheckStopConditions(out var reason)) { Stop(reason); return; }
            StatusText = "Queueing next run";
        }

        // AutoDuty finished its loop count or gave up on its own.
        if (!pendingMeasure && settled && autoDuty.IsStopped() && !autoDuty.IsLooping()
            && DateTime.UtcNow - openWorldSince > TimeSpan.FromSeconds(10))
        {
            Stop("AutoDuty stopped");
        }
    }

    private void RecordRun()
    {
        var now = DateTime.UtcNow;
        RunsCompleted++;
        LastRunGain = Math.Max(0, GetTomestoneCount() - countAtEntry);
        LastRunTime = now - cycleStart; // full cycle: queue + load + duty + exit
        cycleStart = now;
        SessionGained += LastRunGain;
        zeroGainStreak = LastRunGain == 0 ? zeroGainStreak + 1 : 0;

        if (LastRunGain > 0)
        {
            if (!config.DutyStats.TryGetValue(config.TerritoryType, out var stats))
                config.DutyStats[config.TerritoryType] = stats = new DutyStats();
            stats.Runs++;
            stats.Tomes += LastRunGain;
            stats.Seconds += LastRunTime.TotalSeconds;
            stats.DutySeconds += (now - dutyEnteredAt).TotalSeconds;
            config.Save();
        }

        Print($"Run #{RunsCompleted}: +{LastRunGain} in {LastRunTime:mm\\:ss} (now {GetTomestoneCount()}).");
    }

    private bool CheckStopConditions(out string reason)
    {
        var count = GetTomestoneCount();
        var target = Math.Min(config.TargetCount, HoldingCap);
        if (count >= target) { reason = $"Target reached ({count}/{target})"; return true; }
        if (config.MaxRuns > 0 && RunsCompleted >= config.MaxRuns) { reason = $"Run limit reached ({RunsCompleted})"; return true; }
        if (config.StopAfterZeroGainRuns > 0 && zeroGainStreak >= config.StopAfterZeroGainRuns)
        {
            reason = $"{zeroGainStreak} runs with no {ItemDisplayName} (this duty doesn't reward it, or runs are failing)";
            return true;
        }
        reason = string.Empty;
        return false;
    }

    private static bool InOpenWorld()
    {
        var c = Plugin.Condition;
        return Plugin.ClientState.IsLoggedIn
               && !c[ConditionFlag.BoundByDuty]
               && !c[ConditionFlag.BoundByDuty56]
               && !c[ConditionFlag.BetweenAreas]
               && !c[ConditionFlag.BetweenAreas51]
               && !c[ConditionFlag.WatchingCutscene]
               && !c[ConditionFlag.OccupiedInCutSceneEvent];
    }

    private void SetState(FarmState state, string text)
    {
        State = state;
        StatusText = text;
        stateSince = DateTime.UtcNow;
    }

    private static void Print(string msg) => Plugin.ChatGui.Print($"[TomeFarmer] {msg}");
}
