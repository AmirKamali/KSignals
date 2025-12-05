using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

public class User
{
    [Key]
    public ulong Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string FirebaseId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Username { get; set; }

    [MaxLength(255)]
    public string? FirstName { get; set; }

    [MaxLength(255)]
    public string? LastName { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    public bool IsComnEmailOn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
