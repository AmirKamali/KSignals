using System.ComponentModel.DataAnnotations;

namespace kadmin.Models;

public class PlanEditModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string StripePriceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Currency { get; set; } = "usd";

    [Required]
    [MaxLength(32)]
    public string Interval { get; set; } = "month";

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(512)]
    public string? Description { get; set; }
}
