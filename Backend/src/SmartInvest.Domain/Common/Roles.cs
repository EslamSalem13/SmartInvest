namespace SmartInvest.Domain.Common;

/// <summary>
/// أسماء الأدوار المبدئية التي يزرعها النظام عند أول تشغيل.
/// الأدوار نفسها ديناميكية — السوبر أدمن ينشئ ويعدّل الباقي من الواجهة،
/// والتحكم في الوصول يعتمد على <see cref="Permissions"/> وليس على هذه الأسماء.
/// </summary>
public static class Roles
{
    /// <summary>الدور الوحيد الثابت: يملك كل الصلاحيات ويدير باقي الأدوار.</summary>
    public const string SuperAdmin = "SuperAdmin";

    public const string PlanningEmployee = "PlanningEmployee";

    public const string PlanningManager = "PlanningManager";

    public const string FinancialEmployee = "FinancialEmployee";

    public const string FinancialManager = "FinancialManager";
}
