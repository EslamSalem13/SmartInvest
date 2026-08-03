namespace SmartInvest.Domain.Common;

/// <summary>
/// كتالوج الصلاحيات: كل صلاحية مفتاح نصّي "page.action".
/// المفتاح المنتهي بـ .view = الصفحة نفسها، وباقي المفاتيح = أقسام/إجراءات داخل الصفحة.
/// تُخزَّن صلاحيات الدور كـ Role Claims من النوع <see cref="ClaimType"/>.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    // لوحة التحكم
    public const string DashboardView = "dashboard.view";

    // المشروعات
    public const string ProjectsView = "projects.view";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsEdit = "projects.edit";
    public const string ProjectsDelete = "projects.delete";
    public const string ProjectsApprove = "projects.approve";

    // الخطط الاستثمارية
    public const string PlansView = "plans.view";
    public const string PlansManage = "plans.manage";

    // السنوات المالية
    public const string FinancialYearsManage = "financialyears.manage";

    // المقاولون
    public const string ContractorsView = "contractors.view";
    public const string ContractorsManage = "contractors.manage";

    // الجهات التنفيذية
    public const string AgenciesView = "agencies.view";
    public const string AgenciesManage = "agencies.manage";

    // الإدارة المالية — مراحل الطرح
    public const string FinancialView = "financial.view";
    public const string FinancialUpload = "financial.upload";
    public const string FinancialComplete = "financial.complete";

    // مذكرات العرض
    public const string MemosView = "memos.view";
    public const string MemosManage = "memos.manage";

    // إدارة المستخدمين
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";

    // الأدوار والصلاحيات
    public const string RolesManage = "roles.manage";

    /// <summary>الكتالوج مُجمَّعًا حسب الصفحة — تستهلكه واجهة إنشاء الأدوار.</summary>
    public static readonly IReadOnlyList<PermissionGroup> Catalog =
    [
        new("dashboard", "لوحة التحكم",
        [
            new(DashboardView, "عرض لوحة التحكم"),
        ]),
        new("projects", "المشروعات",
        [
            new(ProjectsView, "عرض المشروعات"),
            new(ProjectsCreate, "إضافة مشروع"),
            new(ProjectsEdit, "تعديل مشروع"),
            new(ProjectsDelete, "حذف مشروع"),
            new(ProjectsApprove, "اعتماد المشروع"),
        ]),
        new("plans", "الخطط الاستثمارية",
        [
            new(PlansView, "عرض الخطط"),
            new(PlansManage, "توليد واعتماد الخطة"),
        ]),
        new("financialyears", "السنوات المالية",
        [
            new(FinancialYearsManage, "إدارة السنوات المالية"),
        ]),
        new("contractors", "المقاولون",
        [
            new(ContractorsView, "عرض المقاولين"),
            new(ContractorsManage, "إضافة وتعديل المقاولين"),
        ]),
        new("agencies", "الجهات التنفيذية",
        [
            new(AgenciesView, "عرض الجهات التنفيذية"),
            new(AgenciesManage, "إضافة وتعديل الجهات التنفيذية"),
        ]),
        new("financial", "الإدارة المالية — مراحل الطرح",
        [
            new(FinancialView, "عرض مراحل الطرح"),
            new(FinancialUpload, "رفع إصدارات ومستندات"),
            new(FinancialComplete, "إكمال وإعادة فتح المراحل"),
        ]),
        new("memos", "مذكرات العرض",
        [
            new(MemosView, "عرض المذكرات"),
            new(MemosManage, "إنشاء وتعديل المذكرات"),
        ]),
        new("users", "إدارة المستخدمين",
        [
            new(UsersView, "عرض المستخدمين"),
            new(UsersManage, "إنشاء وتعديل المستخدمين"),
        ]),
        new("roles", "الأدوار والصلاحيات",
        [
            new(RolesManage, "إدارة الأدوار والصلاحيات"),
        ]),
    ];

    /// <summary>كل المفاتيح الصالحة — للتحقق عند إنشاء/تعديل دور، ولمنح السوبر أدمن كل شيء.</summary>
    public static readonly IReadOnlySet<string> All =
        Catalog.SelectMany(g => g.Items).Select(i => i.Key).ToHashSet();
}

public record PermissionItem(string Key, string Label);

public record PermissionGroup(string Key, string Label, IReadOnlyList<PermissionItem> Items);
