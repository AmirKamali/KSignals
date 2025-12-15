using KSignal.API.Models;

namespace kadmin.Models;

public class MarketPriorityPageModel
{
    public MarketPriorityForm Form { get; set; } = new();
    public List<MarketHighPriority> Items { get; set; } = new();
}
