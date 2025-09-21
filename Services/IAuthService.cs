using MyUserApi.Services;

namespace MyUserApi.Services;

public interface IAuthService
{
    string GenerateToken(UserDto user);
}