using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using MyUserApi.Data;
using MyUserApi.Models;

namespace MyUserApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    public UserService(AppDbContext db) => _db = db;

    public async Task<UserDto?> CreateAsync(UserCreateDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email)) return null;

        var user = new User
        {
            Email = dto.Email,
            FullName = dto.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    return new UserDto(user.Id, user.Email, user.FullName, user.CreatedAt, user.PasswordHash);
    }

    public async Task<UserDto?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user == null) return null;
        var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    return ok ? new UserDto(user.Id, user.Email, user.FullName, user.CreatedAt, user.PasswordHash) : null;
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        return await _db.Users.AsNoTracking()
            .Select(u => new UserDto(u.Id, u.Email, u.FullName, u.CreatedAt, u.PasswordHash))
            .ToListAsync();
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        var u = await _db.Users.FindAsync(id);
    return u is null ? null : new UserDto(u.Id, u.Email, u.FullName, u.CreatedAt, u.PasswordHash);
    }
}
