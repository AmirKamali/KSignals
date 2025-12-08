namespace KSignals.DTO;

public class CreateCheckoutRequest
{
    public string PlanCode { get; set; } = string.Empty;
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}
