namespace KSignal.API.Models;

public class MarketSnapshot : MarketSnapshotLatest
{
    public int SettlementTimerSeconds { get; set; }
    public string? EarlyCloseCondition { get; set; }
    public string RulesPrimary { get; set; } = string.Empty;
    public string RulesSecondary { get; set; } = string.Empty;
}
