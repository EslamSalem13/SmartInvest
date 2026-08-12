using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IIdentityService
{
    Task<AuthResultDto> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken cancellationToken = default);

    Task<UserDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);

    Task SetActiveStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task UpdateAvatarAsync(string userId, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAvatarAsync(string userId, CancellationToken cancellationToken = default);

    Task<AvatarDto?> GetAvatarAsync(string userId, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task ResetPasswordByEmailAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
}
