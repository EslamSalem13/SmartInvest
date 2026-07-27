namespace SmartInvest.Application.DTOs.Plan
{
    public class AddAndEditPlanInfoDto
    {
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PlanStatus { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public int FinancialYearId { get; set; }
    }
}
