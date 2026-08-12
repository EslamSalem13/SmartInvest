namespace SmartInvest.Application.DTOs;

public class ExecutionStageDto
{
    public int Id { get; set; }
    public int SubProjectId { get; set; }
    public int? FinancialYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>null فقط لمرحلة التسليم النهائي قبل تسليم الأرضية.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>مرحلة التسليم النهائي المُدارة تلقائيًا — مقفولة في الواجهة.</summary>
    public bool IsFinalDelivery { get; set; }

    /// <summary>موعد هذه المرحلة يتجاوز تاريخ التسليم التعاقدي — تحذير فقط، لا يمنع الحفظ.</summary>
    public bool ExceedsContractualDeadline { get; set; }

    public decimal SelfFundingSpent { get; set; }
    public decimal BankFundingSpent { get; set; }
    public bool HasSelfFundingProof { get; set; }
    public bool HasBankFundingProof { get; set; }
    public string? SelfFundingProofFileName { get; set; }
    public string? BankFundingProofFileName { get; set; }

    public decimal PhysicalProgressPercent { get; set; }
    public bool HasPhysicalProgressProof { get; set; }
    public string? PhysicalProgressProofFileName { get; set; }

    public string? Notes { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public bool PenaltyPaid { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>يُبنى في الـ Controller من multipart/form-data (حقول + حتى 3 ملفات) — نفس نمط UploadProcurementVersionDto.</summary>
public class CreateExecutionStageDto
{
    public int FinancialYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public decimal SelfFundingSpent { get; set; }
    public decimal BankFundingSpent { get; set; }
    public FileUploadDto? SelfFundingProofFile { get; set; }
    public FileUploadDto? BankFundingProofFile { get; set; }
    public decimal PhysicalProgressPercent { get; set; }
    public FileUploadDto? PhysicalProgressProofFile { get; set; }
    public string? Notes { get; set; }
}

public class UpdateExecutionStageDto : CreateExecutionStageDto
{
}

public class SetExecutionStagePenaltyDto
{
    public decimal? PenaltyAmount { get; set; }
    public bool PenaltyPaid { get; set; }
}

/// <summary>صف جدول متابعة المشروعات.</summary>
public class FollowUpListItemDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public string? ContractorName { get; set; }
    public bool IsStalled { get; set; }
    public decimal FinancialProgressPercent { get; set; }
    public decimal PhysicalProgressPercent { get; set; }
    public DateTime? NextDeadline { get; set; }
    public int StageCount { get; set; }
}
