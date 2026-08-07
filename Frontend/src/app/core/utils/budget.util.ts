export interface BudgetParts {
  billions: number;
  millions: number;
  thousands: number;
  units: number;
}

export function splitBudget(value: number | null | undefined): BudgetParts {
  let remaining = Math.round(value ?? 0);
  const billions = Math.floor(remaining / 1_000_000_000);
  remaining -= billions * 1_000_000_000;
  const millions = Math.floor(remaining / 1_000_000);
  remaining -= millions * 1_000_000;
  const thousands = Math.floor(remaining / 1_000);
  remaining -= thousands * 1_000;
  return { billions, millions, thousands, units: remaining };
}

export function combineBudget(parts: BudgetParts): number {
  return (
    (parts.billions || 0) * 1_000_000_000 +
    (parts.millions || 0) * 1_000_000 +
    (parts.thousands || 0) * 1_000 +
    (parts.units || 0)
  );
}
