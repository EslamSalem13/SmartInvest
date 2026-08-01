using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>التقييم الفني — 1:1 مع المشروع الفرعي.</summary>
    public class TechnicalEvaluation : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        public virtual ICollection<TechnicalEvaluationVersion> Versions { get; set; } = new HashSet<TechnicalEvaluationVersion>();
    }
}
