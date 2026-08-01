using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار التقييم المالي — 3 ملفات: محضر فتح المظاريف المالية، تقرير التقييم المالي، مقايسة التكلفة التقديرية.</summary>
    public class FinancialEvaluationVersion : DocumentVersionBase
    {
        public int FinancialEvaluationId { get; set; }
        public virtual FinancialEvaluation FinancialEvaluation { get; set; } = null!;

        /// <summary>محضر فتح المظاريف المالية.</summary>
        public StoredFile? FinancialBidOpeningMinutes { get; set; }

        /// <summary>تقرير التقييم المالي.</summary>
        public StoredFile? FinancialEvaluationReport { get; set; }

        /// <summary>مقايسة التكلفة التقديرية.</summary>
        public StoredFile? EstimatedCostSheet { get; set; }
    }
}
