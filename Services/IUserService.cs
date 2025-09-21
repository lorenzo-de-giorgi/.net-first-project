using MyUserApi.Models;

namespace MyUserApi.Services;

public record UserDto(int Id, string Email, string FullName, DateTime CreatedAt, string PasswordHash);
public record UserCreateDto(string Email, string FullName, string Password);
public record UserLoginDto(string Email, string Password);

public interface IUserService
{
    Task<UserDto?> CreateAsync(UserCreateDto dto);
    Task<UserDto?> ValidateCredentialsAsync(string email, string password);
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(int id);
}
