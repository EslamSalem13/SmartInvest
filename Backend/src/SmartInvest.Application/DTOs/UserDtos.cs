namespace SmartInvest.Application.DTOs;

public class UserDto
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = string.Empty;

    /// <summary>الاسم المعروض للدور بالعربية.</summary>
    public string RoleDisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool HasAvatar { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AvatarDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public string ContentType { get; set; } = string.Empty;
}

public class CreateEmployeeDto
{
    public string FullName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}