using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public AuthController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await _identityService.LoginAsync(dto.UsernameOrEmail, dto.Password, cancellationToken);
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _identityService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _identityService.GetProfileAsync(userId, cancellationToken);
        return Ok(profile);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(UpdateProfileDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _identityService.UpdateProfileAsync(userId, dto, cancellationToken);
        return Ok(profile);
    }

    private static readonly HashSet<string> AllowedAvatarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp",
    };
    private const long MaxAvatarBytes = 2 * 1024 * 1024;

    [HttpPut("me/avatar")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("لم يتم اختيار صورة");
        }

        if (file.Length > MaxAvatarBytes)
        {
            throw new BusinessRuleException("حجم الصورة يجب ألا يتجاوز 2 ميجابايت");
        }

        if (!AllowedAvatarTypes.Contains(file.ContentType))
        {
            throw new BusinessRuleException("صيغة الصورة غير مدعومة، برجاء اختيار PNG أو JPG أو WEBP");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        await _identityService.UpdateAvatarAsync(userId, stream.ToArray(), file.ContentType, cancellationToken);
        return NoContent();
    }

    [HttpDelete("me/avatar")]
    [Authorize]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _identityService.DeleteAvatarAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("users/{userId}/avatar")]
    [Authorize]
    public async Task<IActionResult> GetAvatar(string userId, CancellationToken cancellationToken)
    {
        var avatar = await _identityService.GetAvatarAsync(userId, cancellationToken);
        if (avatar is null)
        {
            return NotFound();
        }

        return File(avatar.Content, avatar.ContentType);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("email-actions")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto, CancellationToken cancellationToken)
    {
        await _identityService.SendPasswordResetAsync(dto.Email, cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("email-actions")]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithTokenDto dto, CancellationToken cancellationToken)
    {
        await _identityService.ResetPasswordByEmailAsync(dto.Email, dto.Token, dto.NewPassword, cancellationToken);
        return NoContent();
    }
}
