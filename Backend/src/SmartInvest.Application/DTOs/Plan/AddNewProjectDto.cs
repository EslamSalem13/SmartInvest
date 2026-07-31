namespace SmartInvest.Application.DTOs.Plan
{
    public class AddNewProjectDto
    {
        public string SubProjectName { get; set; } = string.Empty;
        public int MainProjectId { get; set; }
        public string ProjectLevelName { get; set; } = string.Empty;
        public string ComponentTypeName { get; set; } = string.Empty;
        public string AccountingUnitName { get; set; } = string.Empty;
        public int ProjectLevelId { get; set; }
        public int ComponentTypeId { get; set; }
        public int AccountingUnitId { get; set; }
        public decimal TotalCost { get; set; }
        public string ProjectNature { get; set; } = string.Empty;
        public string? GreenInvestmentLink { get; set; }
        public string? ProjectDescription { get; set; }
        public string? ProjectGoal { get; set; }
        public string? SocialImpact { get; set; }
        public string? EconomicImpact { get; set; }
        public string? EnvironmentalImpact { get; set; }


        public int MarkazId { get; set; }
        public int PriorityId { get; set; }
        public int? ExecutiveAgencyId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int StatusId { get; set; }
    }
}
