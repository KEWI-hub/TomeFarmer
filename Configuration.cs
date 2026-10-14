using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace TomeFarmer;

public sealed class RunLogEntry
{
    public DateTime Finished { get; set; }
    public string Duty { get; set; } = string.Empty;
    public double DutySeconds { get; set; }
    public double CycleSeconds { get; set; }
    public int Tomes { get; set; }
    /// <summary>Seals earned from GC turn-in after the run (net change + seals spent on Duck Bones).</summary>
    public int SealsEarned { get; set; }
    public int DuckBones { get; set; }
}

public sealed class DutyStats
{
    public int Runs { get; set; }
    public int Tomes { get; set; }
    /// <summary>Full cycle time (queue + load + duty), what actually limits tomes/hour.</summary>
    public double Seconds { get; set; }
    /// <summary>Time spent inside the duty only.</summary>
    public double DutySeconds { get; set; }

    public double TomesPerHour => Seconds > 0 ? Tomes / (Seconds / 3600) : 0;
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>TerritoryType id of the duty AutoDuty should run.</summary>
    public uint TerritoryType { get; set; }

    /// <summary>Stop once you hold at least this many tomestones (2000 = holding cap).</summary>
    public int TargetCount { get; set; } = 2000;

    /// <summary>Stop after this many runs. 0 = unlimited.</summary>
    public int MaxRuns { get; set; } = 0;

    /// <summary>Stop when this many runs in a row give no tomestones (weekly cap reached / wrong duty).</summary>
    public int StopAfterZeroGainRuns { get; set; } = 2;

    /// <summary>Pass bareMode to AutoDuty (skips its pre/post loop actions such as repair, turn-ins).</summary>
    public bool BareMode { get; set; } = false;

    /// <summary>0 = auto-detect "Allagan Tomestone of Mathematics" from game data.</summary>
    public uint ItemIdOverride { get; set; } = 0;

    public bool OnlyShowDutiesWithPath { get; set; } = true;
    public int MinDutyLevel { get; set; } = 100;

    /// <summary>Per-duty history keyed by TerritoryType, used to find the fastest farm.</summary>
    public Dictionary<uint, DutyStats> DutyStats { get; set; } = new();

    /// <summary>Newest last; trimmed to <see cref="MaxRunLog"/>.</summary>
    public List<RunLogEntry> RunLog { get; set; } = new();
    public const int MaxRunLog = 300;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
