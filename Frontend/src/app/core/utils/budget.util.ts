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
