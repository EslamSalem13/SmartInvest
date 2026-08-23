using System.Text.RegularExpressions;

namespace SmartInvest.Application.Common;

/// <summary>
/// نص عربي مُطبَّع لمقارنة الأسماء عند مطابقة قيم صفوف الاستيراد (مراكز، برامج، جهات منفذة،
/// مكوّنات عينية، مستويات مشروع، وحدات حسابية...) بالسجلات الموجودة فعليًا في قاعدة البيانات.
///
/// يُزيل التشكيل ويوحّد المسافات الداخلية المتعددة إلى مسافة واحدة، حتى لا يفشل تطابق اسمين
/// متطابقين فعليًا بفارق تشكيل ناقص/زائد أو مسافة إضافية من مصدر الإدخال — نفس المشكلة التي
/// عولجت سابقًا في مطابقة عناوين الأعمدة (<c>ExcelImportParser</c>) لكنها لم تكن مطبَّقة على
/// مطابقة *قيم* الصفوف (اسم المركز، اسم الجهة المنفذة...)، فبقيت مطابقة تلك القيم حساسة لأي
/// فارق نصي بسيط رغم وجود سجل مطابق فعليًا.
///
/// الاستخدام دائمًا للمقارنة/المفاتيح فقط — الاسم المخزَّن فعليًا عند إنشاء سجل جديد يبقى
/// النص الأصلي غير المُطبَّع، حتى لا يُفقَد التشكيل من بيانات المستخدم الحقيقية.
/// </summary>
public static class ArabicTextNormalizer
{
    private static readonly Regex ArabicDiacritics = new("[ً-ٰٟۖ-ۭ]", RegexOptions.Compiled);
    private static readonly Regex MultipleWhitespace = new(@"\s+", RegexOptions.Compiled);

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var withoutDiacritics = ArabicDiacritics.Replace(text, string.Empty);
        return MultipleWhitespace.Replace(withoutDiacritics, " ").Trim();
    }
}
