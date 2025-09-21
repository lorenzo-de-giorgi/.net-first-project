using System.ComponentModel.DataAnnotations;

namespace MyUserApi.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Email { get; set; } = null!;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = null!;

    // Hash della password (mai salvare la password in chiaro)
    [Required]
    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
