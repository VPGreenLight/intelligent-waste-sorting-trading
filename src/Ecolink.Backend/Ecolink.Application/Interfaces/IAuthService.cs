using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<UserInfoDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserInfoDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserInfoDto>> GetAllDemoUsersAsync(CancellationToken cancellationToken = default);
}
