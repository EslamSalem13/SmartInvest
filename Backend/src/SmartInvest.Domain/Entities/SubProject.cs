namespace SmartInvest.Domain.Entities
{
    public class SubProject
    {
        [Key]
        public int SubProjectId { get; set; }

        public int MainProjectId { get; set; }
        public virtual MainProject MainProject { get; set; }
        public string SubProjectName { get; set; } = string.Empty;

        [ForeignKey("ProjectLevel")]
        public int ProjectLevelId { get; set; }
        public virtual ProjectLevel ProjectLevel { get; set; }

        [ForeignKey("ComponentType")]
        public int ComponentTypeId { get; set; }
        public virtual ComponentType ComponentType { get; set; }

        [ForeignKey("AccountingUnit")]
        public int AccountingUnitId { get; set; }
        public virtual AccountingUnit AccountingUnit { get; set; }

        [NotMapped]
        public decimal TotalCost => BankFunding + SelfFunding;
        public string ProjectNature { get; set; } = string.Empty;

        /// <summary>نسبة السماح بتجاوز الميزانية أثناء التنفيذ (مثال: 10 تعني 10%) — على مستوى المشروع الفرعي كله، وليس لكل مرحلة.</summary>
        public decimal? OverrunPercentage { get; set; }

        // Nullables based on ERD
        public string? GreenInvestmentLink { get; set; } 
        public string? ProjectDescription { get; set; }
        public string? ProjectGoal { get; set; }
        public string? SocialImpact { get; set; }
        public string? EconomicImpact { get; set; }
        public string? EnvironmentalImpact { get; set; }

        [ForeignKey("Markaz")]
        public int MarkazId { get; set; }
        public virtual Markaz Markaz { get; set; }

        [ForeignKey("Priority")]
        public int PriorityId { get; set; }
        public virtual ProjectPriority Priority { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        [ForeignKey("Status")]
        public int StatusId { get; set; }
        
        [MaxLength(50)]
        public string? SubProjectCode { get; set; }

        // بيانات الاعتماد
        public bool IsApproved { get; set; }
        [MaxLength(1000)]
        public string? ApprovalCancellationReason { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ApprovalCancelledAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BankFunding { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SelfFunding { get; set; } 
        public virtual ProjectStatus Status { get; set; }

        [ForeignKey("ExecutiveAgency")]
        public int? ExecutiveAgencyId { get; set; }
        public virtual ExecutiveAgency? ExecutiveAgency { get; set; }

        public virtual ICollection<PlanProject> PlanProjects { get; set; }
        public virtual ICollection<SubProjectFinancialYear> FinancialYears { get; set; }
        public virtual ICollection<ProjectAssignment>? ProjectAssignments { get; set; }
        public virtual ICollection<ProjectSpecification>? ProjectSpecifications { get; set; }
    }
}
