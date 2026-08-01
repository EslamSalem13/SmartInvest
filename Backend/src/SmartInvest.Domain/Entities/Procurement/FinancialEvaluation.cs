using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>التقييم المالي — 1:1 مع المشروع الفرعي.</summary>
    public class FinancialEvaluation : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        public virtual ICollection<FinancialEvaluationVersion> Versions { get; set; } = new HashSet<FinancialEvaluationVersion>();
    }
}
