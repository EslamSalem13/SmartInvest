using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار مذكرة عرض — ملف واحد.</summary>
    public class PresentationMemoVersion : DocumentVersionBase
    {
        public int PresentationMemoId { get; set; }
        public virtual PresentationMemo PresentationMemo { get; set; } = null!;

        public StoredFile File { get; set; } = null!;
    }
}
