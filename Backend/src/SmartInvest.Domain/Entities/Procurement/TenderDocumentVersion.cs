using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار كراسة الشروط — ملف واحد.</summary>
    public class TenderDocumentVersion : DocumentVersionBase
    {
        public int TenderDocumentId { get; set; }
        public virtual TenderDocument TenderDocument { get; set; } = null!;

        public StoredFile File { get; set; } = null!;
    }
}
