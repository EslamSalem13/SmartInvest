using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>ربط مذكرة العرض بالمشروعات الفرعية التي تغطيها.</summary>
    public class PresentationMemoSubProject : BaseEntity
    {
        public int PresentationMemoId { get; set; }
        public virtual PresentationMemo PresentationMemo { get; set; } = null!;

        public int SubProjectId { get; set; }
        public virtual SubProject SubProject { get; set; } = null!;
    }
}
