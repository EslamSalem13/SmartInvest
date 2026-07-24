namespace SmartInvest.Domain.Entities
{
    public class ProjectFollowUp
    {
        [Key]
        public int FollowUpId { get; set; }

        [ForeignKey("SubProjectFinancialYear")]
        public int SubProjectFinancialYearId { get; set; }
        public virtual SubProjectFinancialYear SubProjectFinancialYear { get; set; }
        [ForeignKey("Status")]
        public int StatusId { get; set; }
        public virtual ProjectStatus Status { get; set; }
        [ForeignKey("DelayReason")]
        public int? DelayReasonId { get; set; } // Nullable based on ERD
        public virtual DelayReason DelayReason { get; set; }

        public decimal ProgressPercentage { get; set; }
        public DateTime FollowUpDate { get; set; }
        public string? Notes { get; set; }

        public virtual ICollection<ProjectAttachment> Attachments { get; set; }
    }
}
