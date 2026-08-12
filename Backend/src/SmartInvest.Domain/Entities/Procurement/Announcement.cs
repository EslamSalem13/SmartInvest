using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>الإعلان — 1:1 مع المشروع الفرعي.</summary>
    public class Announcement : SubProjectDocumentBase
    {
        /// <summary>
        /// تاريخ نشر الإعلان فعليًا — يُدخله الموظف بنفسه، وقد يختلف عن تاريخ رفع الإصدار.
        /// منه تُحسب مدة الـ15 يومًا الإلزامية قبل إمكان إكمال المرحلة.
        /// </summary>
        public DateTime? AnnouncementDate { get; set; }

        public virtual SubProject SubProject { get; set; } = null!;

        public virtual ICollection<AnnouncementVersion> Versions { get; set; } = new HashSet<AnnouncementVersion>();
    }
}
