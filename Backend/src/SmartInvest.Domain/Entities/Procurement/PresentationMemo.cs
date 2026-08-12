using SmartInvest.Domain.Common;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Domain.Entities
{
    /// <summary>مذكرة عرض — قد تغطي أكثر من مشروع فرعي (M:N).</summary>
    public class PresentationMemo : DocumentBase
    {
        /// <summary>السنة المالية المالكة للمذكرة. null فقط للسجلات القديمة التي تعذّر إسنادها بشكل حتمي.</summary>
        public int? FinancialYearId { get; set; }
        public virtual FinancialYear? FinancialYear { get; set; }

        /// <summary>عنوان تعريفي للمذكرة (إضافة عملية بسيطة فوق الـ ERD لتمييز المذكرات في القوائم).</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// طريقة التعاقد. قابلة للـ null في القاعدة من أجل المذكرات المُنشأة قبل إضافة الحقل،
        /// لكنها إلزامية على أي إنشاء أو تعديل جديد.
        /// </summary>
        public ContractingMethod? ContractingMethod { get; set; }

        public virtual ICollection<PresentationMemoVersion> Versions { get; set; } = new HashSet<PresentationMemoVersion>();

        public virtual ICollection<PresentationMemoSubProject> MemoSubProjects { get; set; } = new HashSet<PresentationMemoSubProject>();
    }
}
