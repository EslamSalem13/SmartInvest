using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>مذكرة عرض — قد تغطي أكثر من مشروع فرعي (M:N).</summary>
    public class PresentationMemo : DocumentBase
    {
        /// <summary>عنوان تعريفي للمذكرة (إضافة عملية بسيطة فوق الـ ERD لتمييز المذكرات في القوائم).</summary>
        public string Title { get; set; } = string.Empty;

        public virtual ICollection<PresentationMemoVersion> Versions { get; set; } = new HashSet<PresentationMemoVersion>();

        public virtual ICollection<PresentationMemoSubProject> MemoSubProjects { get; set; } = new HashSet<PresentationMemoSubProject>();
    }
}
