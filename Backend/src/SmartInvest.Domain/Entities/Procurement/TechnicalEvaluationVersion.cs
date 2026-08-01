using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار التقييم الفني — 3 ملفات: تقرير اللجنة الأول، تقرير اللجنة الثاني، التقرير الفني النهائي.</summary>
    public class TechnicalEvaluationVersion : DocumentVersionBase
    {
        public int TechnicalEvaluationId { get; set; }
        public virtual TechnicalEvaluation TechnicalEvaluation { get; set; } = null!;

        /// <summary>تقرير اللجنة الأول.</summary>
        public StoredFile? FirstCommitteeReport { get; set; }

        /// <summary>تقرير اللجنة الثاني.</summary>
        public StoredFile? SecondCommitteeReport { get; set; }

        /// <summary>تقرير التقييم الفني النهائي.</summary>
        public StoredFile? FinalTechnicalEvaluationReport { get; set; }
    }
}
