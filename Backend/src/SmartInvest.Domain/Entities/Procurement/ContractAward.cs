using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>العقد والترسية — 1:1 مع المشروع الفرعي.</summary>
    public class ContractAward : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        /// <summary>تأكيد صرف الدفعة المقدمة 25% — حالة وليست مستندًا (لا نسخ سابقة لها).</summary>
        public bool AdvancePaymentDone { get; set; }

        public virtual ICollection<ContractAwardVersion> Versions { get; set; } = new HashSet<ContractAwardVersion>();
    }
}
