using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities;

/// <summary>مستند إثبات مرفق بإتاحة بنكية — عدة مستندات لكل إتاحة.</summary>
public class BankAvailabilityDocument
{
    public int BankAvailabilityDocumentId { get; set; }

    public int BankAvailabilityId { get; set; }
    public virtual BankAvailability BankAvailability { get; set; } = null!;

    public StoredFile File { get; set; } = null!;
}
