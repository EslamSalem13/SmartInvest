using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار مذكرة عرض — ملف المذكرة، بالإضافة إلى قرار لجنة الشؤون القانونية المطلوب للإكمال.</summary>
    public class PresentationMemoVersion : DocumentVersionBase
    {
        public int PresentationMemoId { get; set; }
        public virtual PresentationMemo PresentationMemo { get; set; } = null!;

        public StoredFile File { get; set; } = null!;

        /// <summary>
        /// قرار لجنة الشؤون القانونية — إلزامي قبل إكمال المذكرة (وليس قبل رفع الإصدار).
        /// ملاحظة: هذه لجنة الشؤون القانونية، وليست لجنة التقييم الفني (المرحلة الرابعة).
        /// </summary>
        public StoredFile? LegalAffairsCommitteeDecision { get; set; }

        /// <summary>تاريخ رفع قرار لجنة الشؤون القانونية — مطلوب الاحتفاظ به مع المرفق.</summary>
        public DateTime? LegalAffairsDecisionUploadedAt { get; set; }
    }
}
