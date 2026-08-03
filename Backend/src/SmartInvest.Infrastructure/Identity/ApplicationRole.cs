using Microsoft.AspNetCore.Identity;

namespace SmartInvest.Infrastructure.Identity;

/// <summary>
/// دور ديناميكي: السوبر أدمن ينشئ الأدوار ويحدد صلاحياتها من الواجهة.
/// الصلاحيات نفسها تُخزَّن كـ Role Claims (AspNetRoleClaims).
/// </summary>
public class ApplicationRole : IdentityRole
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }

    /// <summary>الاسم المعروض بالعربية.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>أدوار النظام (السوبر أدمن) لا يمكن حذفها أو تعديل صلاحياتها.</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
