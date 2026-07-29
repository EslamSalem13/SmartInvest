namespace SmartInvest.Application.DTOs.Plan
{
    public class PlanWithoutProjectsDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PlanStatus { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public int FinancialYearId { get; set; }
        public string FinancialYearName { get; set; } = string.Empty;
        public DateTime SuggestionDate { get; set; }
    }
}
