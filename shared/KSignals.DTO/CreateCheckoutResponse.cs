namespace KSignals.DTO;

public class CreateCheckoutResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string? PaymentLinkId { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public string? Url { get; set; }
}
