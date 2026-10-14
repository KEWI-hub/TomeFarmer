using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace TomeFarmer;

public sealed class MainWindow : Window
{
    private readonly Configuration config;
    private readonly FarmController farm;
    private readonly AutoDutyIpc autoDuty;

    private List<(uint Territory, string Name, int Level)> duties = new();
    private string dutyFilter = string.Empty;

    public MainWindow(Configuration config, FarmController farm, AutoDutyIpc autoDuty)
        : base("TomeFarmer — Mathematics###TomeFarmerMain")
    {
        this.config = config;
        this.farm = farm;
        this.autoDuty = autoDuty;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 380), MaximumSize = new Vector2(1200, 1200) };
    }

    public override void OnOpen()
    {
        if (duties.Count == 0) RefreshDuties();
    }

    private void RefreshDuties()
    {
        var hasAutoDuty = autoDuty.IsAvailable;
        duties = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(c => c.TerritoryType.RowId != 0 && !c.Name.IsEmpty && c.ClassJobLevelRequired >= config.MinDutyLevel)
            .Select(c => (Territory: c.TerritoryType.RowId, Name: c.Name.ExtractText(), Level: (int)c.ClassJobLevelRequired))
            .Where(d => !config.OnlyShowDutiesWithPath || (hasAutoDuty && autoDuty.ContentHasPath(d.Territory)))
            .DistinctBy(d => d.Territory)
            .OrderByDescending(d => d.Level).ThenBy(d => d.Name)
            .ToList();
    }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Separator();

        using (ImRaii.Disabled(farm.IsRunning))
        {
            DrawDutyPicker();
            ImGui.Separator();
            DrawSettings();
        }

        DrawStats();
    }

    private void DrawStats()
    {
        if (!ImGui.CollapsingHeader("Duty stats (pick the highest tomes/hour)", ImGuiTreeNodeFlags.DefaultOpen)) return;
        if (config.DutyStats.Count == 0) { ImGui.TextDisabled("No runs recorded yet."); return; }

        using var table = ImRaii.Table("##stats", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (!table) return;
        ImGui.TableSetupColumn("Duty");
        ImGui.TableSetupColumn("Runs");
        ImGui.TableSetupColumn("Tome/run");
        ImGui.TableSetupColumn("In duty");
        ImGui.TableSetupColumn("Cycle");
        ImGui.TableSetupColumn("Tome/hr");
        ImGui.TableHeadersRow();

        var cfc = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>();
        foreach (var (territory, s) in config.DutyStats.OrderByDescending(kv => kv.Value.TomesPerHour))
        {
            var name = cfc.FirstOrDefault(c => c.TerritoryType.RowId == territory).Name.ExtractText();
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted(string.IsNullOrEmpty(name) ? territory.ToString() : name);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(s.Runs.ToString());
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{(double)s.Tomes / s.Runs:0}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{TimeSpan.FromSeconds(s.DutySeconds / s.Runs):mm\\:ss}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{TimeSpan.FromSeconds(s.Seconds / s.Runs):mm\\:ss}");
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{s.TomesPerHour:0}");
        }

        if (ImGui.SmallButton("Reset stats")) { config.DutyStats.Clear(); config.Save(); }
    }

    private void DrawStatus()
    {
        var available = autoDuty.IsAvailable;
        ImGui.TextColored(available ? new Vector4(0.4f, 1f, 0.4f, 1f) : new Vector4(1f, 0.4f, 0.4f, 1f),
            available ? "AutoDuty: connected" : "AutoDuty: NOT loaded");

        var count = farm.GetTomestoneCount();
        var target = Math.Min(config.TargetCount, FarmController.HoldingCap);
        ImGui.TextUnformatted($"{farm.ItemDisplayName} (id {farm.ItemId}): {count} / {target}");
        ImGui.ProgressBar(target > 0 ? Math.Clamp(count / (float)target, 0f, 1f) : 0f, new Vector2(-1, 0));

        ImGui.TextUnformatted($"Status: {farm.StatusText}");
        ImGui.TextUnformatted($"Runs: {farm.RunsCompleted}   Gained: {farm.SessionGained}   Last run: +{farm.LastRunGain} in {farm.LastRunTime:mm\\:ss}");
        if (farm.SessionStart is { } start && farm.RunsCompleted > 0)
        {
            var hours = (DateTime.UtcNow - start).TotalHours;
            if (hours > 0) ImGui.TextUnformatted($"Rate: {farm.SessionGained / hours:0} / hour");
        }

        if (!farm.IsRunning)
        {
            if (ImGui.Button("Start farming", new Vector2(160, 0))) farm.Start();
        }
        else
        {
            if (ImGui.Button("Stop", new Vector2(160, 0))) farm.Stop();
        }
    }

    private void DrawDutyPicker()
    {
        var current = duties.FirstOrDefault(d => d.Territory == config.TerritoryType);
        ImGui.TextUnformatted($"Duty: {(current.Territory != 0 ? $"{current.Name} (Lv{current.Level})" : config.TerritoryType != 0 ? $"territory {config.TerritoryType}" : "none")}");

        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##filter", "Search duty…", ref dutyFilter, 64);
        ImGui.SameLine();
        if (ImGui.Button("Refresh list")) RefreshDuties();

        using (var child = ImRaii.Child("##duties", new Vector2(-1, 140), true))
        {
            if (child)
            {
                foreach (var d in duties.Where(d => dutyFilter.Length == 0 || d.Name.Contains(dutyFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    if (ImGui.Selectable($"Lv{d.Level}  {d.Name}##{d.Territory}", d.Territory == config.TerritoryType))
                    {
                        config.TerritoryType = d.Territory;
                        config.Save();
                    }
                }
            }
        }
        ImGui.TextDisabled("Duty mode (Trust / Duty Support / Regular) is set in AutoDuty's own window.");
    }

    private void DrawSettings()
    {
        var changed = false;

        var target = config.TargetCount;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Target count (max 2000)", ref target, 50)) { config.TargetCount = Math.Clamp(target, 1, FarmController.HoldingCap); changed = true; }

        var maxRuns = config.MaxRuns;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Max runs (0 = no limit)", ref maxRuns)) { config.MaxRuns = Math.Max(0, maxRuns); changed = true; }

        var zero = config.StopAfterZeroGainRuns;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Stop after N runs with +0", ref zero)) { config.StopAfterZeroGainRuns = Math.Max(0, zero); changed = true; }

        var bare = config.BareMode;
        if (ImGui.Checkbox("AutoDuty bare mode (skip repair/turn-in etc.)", ref bare)) { config.BareMode = bare; changed = true; }

        if (ImGui.CollapsingHeader("Advanced"))
        {
            var onlyPath = config.OnlyShowDutiesWithPath;
            if (ImGui.Checkbox("Only list duties AutoDuty has a path for", ref onlyPath)) { config.OnlyShowDutiesWithPath = onlyPath; changed = true; RefreshDuties(); }

            var minLvl = config.MinDutyLevel;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Min duty level", ref minLvl)) { config.MinDutyLevel = Math.Clamp(minLvl, 1, 100); changed = true; }

            var idOverride = (int)config.ItemIdOverride;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Item id override (0 = auto)", ref idOverride))
            {
                config.ItemIdOverride = (uint)Math.Max(0, idOverride);
                changed = true;
                farm.ResolveItem();
            }
        }

        if (changed) config.Save();
    }
}
