namespace SmartInvest.Domain.Entities
{
    public class SubProjectFinancialYear
    {
        [Key]
        public int SubProjectFinancialYearId { get; set; }

        [ForeignKey("SubProject")]
        public int SubProjectId { get; set; }
        public virtual SubProject SubProject { get; set; }

        [ForeignKey("FinancialYear")]
        public int FinancialYearId { get; set; }
        public virtual FinancialYear FinancialYear { get; set; }

        public virtual ICollection<ProjectFollowUp> ProjectFollowUps { get; set; }
        public virtual ICollection<ExecutionStage> ExecutionStages { get; set; } = new HashSet<ExecutionStage>();
    }
}
