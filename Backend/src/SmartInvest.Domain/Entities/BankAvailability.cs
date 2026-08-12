using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvest.Domain.Entities;

/// <summary>
/// سجل إتاحة بنكية — دفعة أموال أتاحها البنك للسنة المالية. يمكن تعديلها أو حذفها
/// طالما السنة المالية غير مقفولة (التعديل للإدارة المالية، والحذف لمديرها والسوبر أدمن فقط).
/// </summary>
public class BankAvailability
{
    [Key]
    public int BankAvailabilityId { get; set; }

    public int FinancialYearId { get; set; }
    public virtual FinancialYear FinancialYear { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>تاريخ استلام/إتاحة المبلغ من البنك.</summary>
    public DateTime ReceivedDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public virtual ICollection<BankAvailabilityDocument> Documents { get; set; } = new List<BankAvailabilityDocument>();
}
