using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>فتح المظاريف — 1:1 مع المشروع الفرعي.</summary>
    public class OpeningEnvelopes : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        public virtual ICollection<OpeningEnvelopesVersion> Versions { get; set; } = new HashSet<OpeningEnvelopesVersion>();
    }
}
