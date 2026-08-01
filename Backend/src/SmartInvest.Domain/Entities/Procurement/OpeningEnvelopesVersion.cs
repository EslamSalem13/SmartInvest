using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار محضر فتح المظاريف — ملف واحد.</summary>
    public class OpeningEnvelopesVersion : DocumentVersionBase
    {
        public int OpeningEnvelopesId { get; set; }
        public virtual OpeningEnvelopes OpeningEnvelopes { get; set; } = null!;

        public StoredFile File { get; set; } = null!;
    }
}
