using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار العقد والترسية — ملفان: أمر الإسناد، العقد (الدفعة المقدمة 25% أصبحت حالة على المستند، راجع ContractAward.AdvancePaymentDone).</summary>
    public class ContractAwardVersion : DocumentVersionBase
    {
        public int ContractAwardId { get; set; }
        public virtual ContractAward ContractAward { get; set; } = null!;

        /// <summary>أمر الإسناد (الترسية).</summary>
        public StoredFile? AwardOrder { get; set; }

        /// <summary>العقد الموقَّع.</summary>
        public StoredFile? Contract { get; set; }
    }
}
