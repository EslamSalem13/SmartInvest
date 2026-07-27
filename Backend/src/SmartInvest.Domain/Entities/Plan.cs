using SmartInvest.Domain.Enums;

namespace SmartInvest.Domain.Entities
{
    public class Plan
    {
        [Key]
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public PlanStatus PlanStatus { get; set; } 
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
         
        public DateTime SuggestionDate { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovalDate { get; set; }

        [ForeignKey("FinancialYear")]
        public int FinancialYearId { get; set; }
        public virtual FinancialYear? FinancialYear { get; set; }

        public virtual ICollection<PlanProject>? PlanProjects { get; set; }
    }
}
