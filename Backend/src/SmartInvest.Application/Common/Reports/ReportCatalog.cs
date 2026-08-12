using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Common.Reports;

public static class ReportCatalog
{
    private static readonly IReadOnlyList<ReportCatalogItemDto> Reports = new List<ReportCatalogItemDto>
    {
        Create("project-register", "السجل الشامل للمشروعات", "بيانات المشروعات التخطيطية والمالية والجغرافية والتصنيفية في كشف موحّد.",
            "السنة المالية", "الأكواد والأسماء", "البرامج", "نوع ومستوى المشروع", "الموقع", "الجهة والأولوية والحالة", "التمويل", "الأهداف والآثار"),
        Create("funding-vs-spending", "التمويل مقابل المصروف والمتبقي", "مقارنة الاعتمادات المالية بالمصروف الفعلي ونسب التنفيذ والتجاوزات.",
            "التمويل البنكي والذاتي", "المصروف", "المتبقي", "نسبة الصرف", "التقدم العيني", "الجزاءات"),
        Create("bank-availability-ledger", "سجل الإتاحات البنكية", "حركة الإتاحات البنكية والرصيد التراكمي ومستندات الإثبات لكل سنة مالية.",
            "قيمة الإتاحة", "تاريخ الاستلام والتسجيل", "الرصيد التراكمي", "المنشئ", "المستندات", "العجز أو المتبقي"),
        Create("plan-approval-status", "موقف الخطط والاعتمادات", "حالة الخطط والمشروعات المدرجة بها وتواريخ الاعتماد أو الإلغاء.",
            "الخطة والسنة", "حالة الخطة", "تواريخ الاقتراح والاعتماد", "المشروع", "حالة اعتماد المشروع", "التمويل"),
        Create("procurement-pipeline", "موقف مراحل الطرح والتعاقد", "الموقف التنفيذي لمذكرة العرض ومراحل الطرح الست لكل مشروع.",
            "مذكرة العرض", "كراسة الشروط", "الإعلان", "فتح المظاريف", "التقييم الفني", "التقييم المالي", "العقد والترسية"),
        Create("contracts-contractors", "العقود والترسية والمقاولون", "تفاصيل العقود والمقاولين والدفعات المقدمة ومدد التنفيذ والجزاءات.",
            "المقاول", "نوع ورقم وقيمة العقد", "مدة التنفيذ", "تسليم الأرضية", "الدفعة المقدمة", "الجزاءات", "تقييم المقاول"),
        Create("execution-delays", "التنفيذ والتأخيرات والجزاءات", "مراحل التنفيذ ومواعيدها ونسب الإنجاز والمصروف والتأخيرات والجزاءات.",
            "مرحلة التنفيذ", "الموعد", "أيام التأخير", "التقدم العيني", "المصروف", "الجزاء", "الحالة"),
        Create("geographic-distribution", "التوزيع الجغرافي للمشروعات", "تجميع المشروعات والتمويل والإنجاز حسب المحافظة والمركز.",
            "المحافظة والمركز", "أعداد المشروعات", "حالات التنفيذ", "التمويل", "المصروف", "متوسط الإنجاز"),
        Create("program-agency-performance", "أداء البرامج والجهات التنفيذية", "مؤشرات أداء البرامج والجهات التنفيذية من حيث التمويل والتنفيذ والتأخير.",
            "البرنامج الرئيسي والفرعي", "الجهة التنفيذية", "أعداد المشروعات", "نوع المشروع", "التمويل والمصروف", "الإنجاز والتأخير"),
        Create("measurements-outcomes", "القياسات والمخرجات العينية", "تفاصيل القياسات والكميات والوحدات المسجلة للمشروعات.",
            "المشروع والبرنامج", "الموقع", "القياس", "الوحدة", "القيمة", "نوع المشروع", "التمويل")
    }.AsReadOnly();

    public static IReadOnlyList<ReportCatalogItemDto> GetAll()
    {
        return Reports;
    }

    public static ReportCatalogItemDto? Find(string key)
    {
        return Reports.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static ReportCatalogItemDto Create(string key, string title, string description, params string[] includedFields)
    {
        return new ReportCatalogItemDto
        {
            Key = key,
            Title = title,
            Description = description,
            IncludedFields = includedFields.ToList()
        };
    }
}
