namespace SmartInvest.Application.DTOs.Plan
{
    public class PlanInfoDto
    {
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PlanStatus { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public List<AddNewProjectDto>? Projects { get; set; }
    }
}
