namespace SmartInvest.Application.DTOs.Plan
{
    public class ProjectInfoDto
    {
        public int SubProjectId { get; set; }

        public string SubProjectName { get; set; } = string.Empty;  
        public string ProjectLevel { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }

        public string? ExecutiveAgencyName { get; set; }
    }
}
