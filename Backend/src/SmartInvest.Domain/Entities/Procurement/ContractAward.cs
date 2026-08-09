using System.ComponentModel.DataAnnotations.Schema;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Domain.Entities
{
    /// <summary>العقد والترسية — 1:1 مع المشروع الفرعي.</summary>
    public class ContractAward : SubProjectDocumentBase
    {
        public virtual SubProject SubProject { get; set; } = null!;

        // ===== إسناد المقاول =====

        /// <summary>
        /// الإسناد الناتج عن هذه الترسية. مصدر الحقيقة لهوية المقاول هو
        /// <see cref="ProjectAssignment"/> نفسه — هذا مجرد ربط للرجوع إليه.
        /// </summary>
        public int? ProjectAssignmentId { get; set; }
        public virtual ProjectAssignment? ProjectAssignment { get; set; }

        // ===== الدفعة المقدمة =====
        // لا تُصرف إطلاقًا لمشروعات «توريدات» — المقاول هو من يورّد أولًا ثم يُصرف له.
        // في «مقاولات» النسبة حرة يحددها الموظف (وليست 25% ثابتة كما كان سابقًا).

        /// <summary>تأكيد الموظف بصرف الدفعة المقدمة.</summary>
        public bool AdvancePaymentDone { get; set; }

        /// <summary>نسبة الدفعة المقدمة من إجمالي تكلفة المشروع.</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AdvancePaymentPercentage { get; set; }

        /// <summary>الجزء المصروف من التمويل الذاتي.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AdvancePaymentSelfAmount { get; set; }

        /// <summary>الجزء المصروف من التمويل البنكي.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AdvancePaymentBankAmount { get; set; }

        // ===== مدة التنفيذ وتسليم الأرضية =====

        /// <summary>الحد الأقصى لمدة التنفيذ — الشهور.</summary>
        public int? ExecutionDurationMonths { get; set; }

        /// <summary>الحد الأقصى لمدة التنفيذ — الأيام (بالإضافة إلى الشهور).</summary>
        public int? ExecutionDurationDays { get; set; }

        /// <summary>هل الأرضية مُسلَّمة وقت الترسية أم لاحقًا؟</summary>
        public SiteHandoverMode? SiteHandoverMode { get; set; }

        /// <summary>
        /// تاريخ تسليم الأرضية للمقاول — بداية احتساب المدة.
        /// يُملأ عند إكمال الترسية في وضع <see cref="Enums.SiteHandoverMode.AtAward"/>،
        /// أو لاحقًا من متابعة المشروعات في وضع <see cref="Enums.SiteHandoverMode.Pending"/>.
        /// </summary>
        public DateTime? SiteHandoverDate { get; set; }

        /// <summary>إثبات تسليم الأرضية للمقاول (PDF أو صورة) — إلزامي عند تسجيل التسليم.</summary>
        public StoredFile? SiteHandoverProofFile { get; set; }

        /// <summary>
        /// تاريخ التسليم النهائي المستحق — محسوب وغير مخزَّن،
        /// حتى لا يتعارض مع تصحيح تاريخ تسليم الأرضية بعد إدخاله.
        /// </summary>
        [NotMapped]
        public DateTime? ContractualDeliveryDate =>
            SiteHandoverDate?
                .AddMonths(ExecutionDurationMonths ?? 0)
                .AddDays(ExecutionDurationDays ?? 0);

        // ===== الشرط الجزائي =====

        /// <summary>قيمة الشرط الجزائي عند تجاوز تاريخ التسليم — مبلغ يُدخله الموظف يدويًا.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PenaltyAmount { get; set; }

        public virtual ICollection<ContractAwardVersion> Versions { get; set; } = new HashSet<ContractAwardVersion>();
    }
}
