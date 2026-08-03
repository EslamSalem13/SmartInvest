using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Common;

/// <summary>البادئة المستخدمة لتحويل مفتاح الصلاحية إلى اسم Policy.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string For(string permission) => Prefix + permission;
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}

/// <summary>
/// يتحقق أن المستخدم يملك claim من نوع "permission" بالمفتاح المطلوب.
/// السوبر أدمن يعدّي كل الفحوصات.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.IsInRole(Roles.SuperAdmin) ||
            context.User.HasClaim(Permissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// يبني سياسات الصلاحيات ديناميكيًا من اسم الـ Policy ("perm:projects.view")
/// بدلًا من تسجيل عشرات السياسات يدويًا.
/// </summary>
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existing = await base.GetPolicyAsync(policyName);
        if (existing != null)
        {
            return existing;
        }

        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var permission = policyName[PermissionPolicy.Prefix.Length..];

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}

/// <summary>اختصار: [HasPermission(Permissions.ProjectsView)]</summary>
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = PermissionPolicy.For(permission);
}
