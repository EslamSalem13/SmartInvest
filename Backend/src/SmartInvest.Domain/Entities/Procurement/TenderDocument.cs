using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>كراسة الشروط — 1:1 مع المشروع الفرعي. الملحق = إصدار جديد.</summary>
    public class TenderDocument : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        public virtual ICollection<TenderDocumentVersion> Versions { get; set; } = new HashSet<TenderDocumentVersion>();
    }
}
