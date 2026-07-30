namespace SmartInvest.Domain.Common;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";

    public const string PlanningEmployee = "PlanningEmployee";

    public const string PlanningManager = "PlanningManager";

    /// <summary>مدير + موظف تخطيط.</summary>
    public const string PlanningStaff = "PlanningEmployee,PlanningManager";
}