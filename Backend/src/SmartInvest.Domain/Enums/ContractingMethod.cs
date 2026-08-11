namespace SmartInvest.Domain.Enums;

/// <summary>
/// طريقة التعاقد المتبعة في مذكرة العرض.
/// مرتّبة من الأقل تنافسية (إسناد مباشر) إلى الأكثر تنافسية (مناقصة ذات مرحلتين).
/// </summary>
public enum ContractingMethod
{
    /// <summary>إسناد مباشر</summary>
    DirectAssignment = 1,

    /// <summary>الاتفاق المباشر</summary>
    DirectAgreement = 2,

    /// <summary>ممارسة محدودة</summary>
    LimitedPractice = 3,

    /// <summary>الممارسة العامة</summary>
    GeneralPractice = 4,

    /// <summary>مناقصة خاصة</summary>
    PrivateTender = 5,

    /// <summary>مناقصة عامة</summary>
    PublicTender = 6,

    /// <summary>المناقصة ذات المرحلتين</summary>
    TwoStageTender = 7,
}

/// <summary>التسميات العربية لطرق التعاقد — مصدر واحد يقرأ منه الـ API والواجهة.</summary>
public static class ContractingMethodLabels
{
    private static readonly Dictionary<ContractingMethod, string> Labels = new()
    {
        [ContractingMethod.DirectAssignment] = "إسناد مباشر",
        [ContractingMethod.DirectAgreement] = "الاتفاق المباشر",
        [ContractingMethod.LimitedPractice] = "ممارسة محدودة",
        [ContractingMethod.GeneralPractice] = "الممارسة العامة",
        [ContractingMethod.PrivateTender] = "مناقصة خاصة",
        [ContractingMethod.PublicTender] = "مناقصة عامة",
        [ContractingMethod.TwoStageTender] = "المناقصة ذات المرحلتين",
    };

    public static bool IsDefined(int value) => Labels.ContainsKey((ContractingMethod)value);

    public static string? ToLabel(ContractingMethod? method) =>
        method is { } m && Labels.TryGetValue(m, out var label) ? label : null;
}
