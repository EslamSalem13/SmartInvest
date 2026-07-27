using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Common;

/// <summary>
/// حساب الـ SuperAdmin يعدّي أي فحص صلاحيات مبني على الأدوار ([Authorize(Roles = ...)])
/// بدون الحاجة لإضافة SuperAdmin يدويًا لكل تحكم/إجراء في المشروع.
/// </summary>
public class SuperAdminAuthorizationHandler : AuthorizationHandler<RolesAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RolesAuthorizationRequirement requirement)
    {
        if (context.User.IsInRole(Roles.SuperAdmin))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
