import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import { FinancialYear } from '../../core/models/project.models';
import { egpToThousands, thousandsToEgp } from '../../core/utils/budget.util';

@Component({
  selector: 'app-financial-years-settings',
  imports: [FormsModule],
  templateUrl: './financial-years-settings.html',
  styleUrl: './financial-years-settings.css',
})
export class FinancialYearsSettings {
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);
  protected readonly isManager = this.auth.isManager;

  protected readonly years = signal<FinancialYear[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly showForm = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly formName = signal('');
  protected readonly formStartDate = signal('');
  protected readonly formEndDate = signal('');
  protected readonly formBudgetThousands = signal<number | null>(null);
  protected readonly formBudgetTotal = computed(() => thousandsToEgp(this.formBudgetThousands()));
  protected readonly formIsClosed = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.years.set(years);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل السنوات المالية');
        this.loading.set(false);
      },
    });
  }

  protected openEdit(year: FinancialYear): void {
    this.editingId.set(year.id);
    this.formName.set(year.name);
    this.formStartDate.set(year.startDate.slice(0, 10));
    this.formEndDate.set(year.endDate.slice(0, 10));
    this.formBudgetThousands.set(egpToThousands(year.budget));
    this.formIsClosed.set(year.isClosed);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    if (this.saving()) return;
    this.showForm.set(false);
  }

  protected updateBudgetThousands(value: number | string): void {
    if (value === '' || value == null) {
      this.formBudgetThousands.set(null);
      return;
    }
    const num = Number(value);
    this.formBudgetThousands.set(Number.isNaN(num) || num < 0 ? null : num);
  }

  protected submitForm(): void {
    if (this.saving()) return;
    const id = this.editingId();
    if (id == null) return;

    const name = this.formName().trim();
    if (!name || !this.formStartDate() || !this.formEndDate()) return;

    this.saving.set(true);
    this.formError.set(null);
    const budget = thousandsToEgp(this.formBudgetThousands());
    this.financialYearsService
      .update(id, {
        name,
        startDate: this.formStartDate(),
        endDate: this.formEndDate(),
        isClosed: this.formIsClosed(),
        budget: budget > 0 ? budget : null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showForm.set(false);
          this.load();
        },
        error: (err) => {
          this.saving.set(false);
          this.formError.set(err?.error?.message ?? 'تعذّر حفظ التعديلات');
        },
      });
  }

  protected money(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString('en-US');
  }
}
