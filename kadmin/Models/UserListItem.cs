using KSignal.API.Models;

namespace kadmin.Models;

public class UserListItem
{
    public User User { get; set; } = new();
    public string? ActivePlanName { get; set; }
}
