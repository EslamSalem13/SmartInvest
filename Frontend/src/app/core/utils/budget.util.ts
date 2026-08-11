/** يحوّل قيمة مُدخلة بالألف جنيه إلى جنيه كامل (للإرسال إلى الـ API). */
export function thousandsToEgp(thousands: number | null | undefined): number {
  if (thousands == null || Number.isNaN(thousands) || thousands < 0) {
    return 0;
  }
  return Math.round(thousands * 1000 * 100) / 100;
}

/** يحوّل قيمة مخزّنة بالجنيه إلى الألف جنيه (لعرضها في الحقل عند التعديل). دقة لأقرب جنيه حتى لا تُفقد قيم غير مضاعفة للعشرة عند العرض. */
export function egpToThousands(egp: number | null | undefined): number | null {
  if (egp == null) {
    return null;
  }
  return Math.round(egp) / 1000;
}

/**
 * يعرض قيمة مخزّنة بالجنيه الكامل كنص "بالألف جنيه" — للعرض فقط، لا يُستخدم عند الحفظ
 * ولا يغيّر القيمة الأصلية القادمة من الـ API. يحتفظ بالكسور العشرية (حتى 3 منازل) دون
 * أصفار زائدة، مثل: 703500000 → "703,500 ألف ج.م"، 125500 → "125.5 ألف ج.م".
 */
export function formatEgpAsThousands(value: number | null | undefined): string {
  const thousands = (value ?? 0) / 1000;
  return `${thousands.toLocaleString('en-US', { maximumFractionDigits: 3 })} ألف ج.م`;
}
