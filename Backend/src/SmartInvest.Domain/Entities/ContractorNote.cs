namespace SmartInvest.Domain.Entities;

/// <summary>ملاحظة عامة عن المقاول (SubProjectId فارغ) أو مرتبطة بمشروع بعينه.</summary>
public class ContractorNote
{
    public int ContractorNoteId { get; set; }

    public int ContractorId { get; set; }
    public virtual Contractor Contractor { get; set; } = null!;

    public int? SubProjectId { get; set; }
    public virtual SubProject? SubProject { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>true لو كتبها الذكاء الاصطناعي (تقرير مستقبلي) بدل موظف.</summary>
    public bool IsAiGenerated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
