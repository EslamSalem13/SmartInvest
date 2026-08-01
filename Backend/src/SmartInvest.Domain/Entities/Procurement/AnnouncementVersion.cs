using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار الإعلان — 3 ملفات: إعلان الجريدة، إعلان البوابة، موافقة الجهة المختصة.</summary>
    public class AnnouncementVersion : DocumentVersionBase
    {
        public int AnnouncementId { get; set; }
        public virtual Announcement Announcement { get; set; } = null!;

        /// <summary>صورة إعلان الجريدة.</summary>
        public StoredFile? NewspaperAdvertisement { get; set; }

        /// <summary>صورة إعلان بوابة المشتريات.</summary>
        public StoredFile? PortalAdvertisement { get; set; }

        /// <summary>موافقة الجهة المختصة.</summary>
        public StoredFile? CompetentAuthorityApproval { get; set; }
    }
}
