using System.ComponentModel.DataAnnotations;

namespace kadmin.Models;

public class UserEditModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string FirebaseId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Username { get; set; }

    [MaxLength(255)]
    public string? FirstName { get; set; }

    [MaxLength(255)]
    public string? LastName { get; set; }

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    public bool IsComnEmailOn { get; set; }

    public Guid? ActiveSubscriptionId { get; set; }

    public Guid? ActivePlanId { get; set; }

    [MaxLength(64)]
    public string SubscriptionStatus { get; set; } = "none";

    [MaxLength(255)]
    public string? StripeCustomerId { get; set; }
}
