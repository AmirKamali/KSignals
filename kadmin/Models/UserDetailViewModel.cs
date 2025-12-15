using KSignal.API.Models;
using KSignals.DTO;

namespace kadmin.Models;

public class UserDetailViewModel
{
    public UserEditModel Form { get; set; } = new();
    public UserProfileResponse Profile { get; set; } = new();
    public IEnumerable<UserSubscription> Subscriptions { get; set; } = Enumerable.Empty<UserSubscription>();
    public IEnumerable<SubscriptionEventLog> Events { get; set; } = Enumerable.Empty<SubscriptionEventLog>();
    public IEnumerable<SubscriptionPlan> Plans { get; set; } = Enumerable.Empty<SubscriptionPlan>();
}
