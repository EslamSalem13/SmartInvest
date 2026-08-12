namespace SmartInvest.Domain.Common;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";

    public const string PlanningEmployee = "PlanningEmployee";

    public const string PlanningManager = "PlanningManager";

    public const string FinancialEmployee = "FinancialEmployee";

    public const string FinancialManager = "FinancialManager";

    /// <summary>مدير + موظف تخطيط.</summary>
    public const string PlanningStaff = "PlanningEmployee,PlanningManager";

    /// <summary>مدير التخطيط + سوبر أدمن — للعمليات الإدارية الحساسة مثل حذف الإتاحات البنكية.</summary>
    public const string ManagementStaff = "PlanningManager,SuperAdmin";

    /// <summary>مدير وموظف الإدارة المالية.</summary>
    public const string FinancialStaff = "FinancialEmployee,FinancialManager";

    /// <summary>الموظفون المسموح لهم بتنفيذ أعمال الإدارة المالية اليومية.</summary>
    public const string FinancialOperationsStaff = "PlanningEmployee,PlanningManager,FinancialEmployee,FinancialManager";

    /// <summary>المديرون المسموح لهم بتنفيذ العمليات المالية الحساسة.</summary>
    public const string FinancialManagers = "PlanningManager,FinancialManager,SuperAdmin";
}
