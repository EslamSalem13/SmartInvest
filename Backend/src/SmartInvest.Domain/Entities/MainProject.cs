namespace SmartInvest.Domain.Entities
{
    public class MainProject
    {
        [Key]
        public int MainProjectId { get; set; }
        [MaxLength(50)]
        public string? MainProjectCode { get; set; }
        public bool IsApproved { get; set; }
        public string MainProjectName { get; set; } = string.Empty;
        public string ExecutingAgency { get; set; } = string.Empty;
        [ForeignKey("SubProgram")]
        public int SubProgramId { get; set; }
        public virtual SubProgram SubProgram { get; set; }
        public virtual ICollection<SubProject> SubProjects { get; set; }
    }
}
