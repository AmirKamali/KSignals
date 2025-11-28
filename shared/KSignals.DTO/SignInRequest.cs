namespace KSignals.DTO;

public class SignInRequest
{
    public string FirebaseId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public bool IsComnEmailOn { get; set; }
}
