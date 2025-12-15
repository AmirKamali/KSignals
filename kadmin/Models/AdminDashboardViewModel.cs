using KSignal.API.Models;
using KSignals.DTO;

namespace kadmin.Models;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int Plans { get; set; }
    public int HighPriorityCount { get; set; }
    public List<MarketHighPriority> HighPriority { get; set; } = new();
    public List<SubscriptionEventViewModel> LatestEvents { get; set; } = new();
}

public class SubscriptionEventViewModel
{
    public string UserLabel { get; set; } = string.Empty;
    public SubscriptionEventDto Event { get; set; } = new();
}
