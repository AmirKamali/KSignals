using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

public class TagsCategory
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Category { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Tag { get; set; } = string.Empty;
    
    public DateTime LastUpdate { get; set; }
    
    public bool IsDeleted { get; set; }
}
