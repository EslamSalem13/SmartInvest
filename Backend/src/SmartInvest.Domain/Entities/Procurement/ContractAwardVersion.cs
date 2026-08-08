using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities
{
    /// <summary>إصدار العقد والترسية — أمر الإسناد، العقد، وإثبات صرف الدفعة المقدمة (بيانات الدفعة نفسها حالة على المستند، راجع ContractAward).</summary>
    public class ContractAwardVersion : DocumentVersionBase
    {
        public int ContractAwardId { get; set; }
        public virtual ContractAward ContractAward { get; set; } = null!;

        /// <summary>أمر الإسناد (الترسية).</summary>
        public StoredFile? AwardOrder { get; set; }

        /// <summary>العقد الموقَّع.</summary>
        public StoredFile? Contract { get; set; }

        /// <summary>إثبات صرف الدفعة المقدمة — مطلوب لمشروعات «مقاولات» فقط.</summary>
        public StoredFile? AdvancePaymentProof { get; set; }
    }
}
