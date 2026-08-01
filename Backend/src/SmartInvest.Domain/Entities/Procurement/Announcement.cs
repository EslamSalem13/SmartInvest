using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>الإعلان — 1:1 مع المشروع الفرعي.</summary>
    public class Announcement : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        public virtual ICollection<AnnouncementVersion> Versions { get; set; } = new HashSet<AnnouncementVersion>();
    }
}
