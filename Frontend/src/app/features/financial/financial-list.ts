import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FinancialService } from '../../core/services/financial.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { ProcurementSubProjectListItem } from '../../core/models/financial.models';
import { FinancialYear } from '../../core/models/project.models';

@Component({
  selector: 'app-financial-list',
  imports: [RouterLink, FormsModule],
  templateUrl: './financial-list.html',
  styleUrl: './financial.css',
})
export class FinancialList implements OnInit {
  private readonly financial = inject(FinancialService);
  private readonly financialYearsService = inject(FinancialYearsService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<ProcurementSubProjectListItem[]>([]);
  protected readonly search = signal('');

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  protected readonly filtered = computed(() => {
    const term = this.search().trim();
    if (!term) {
      return this.items();
    }
    return this.items().filter(
      (x) =>
        x.subProjectName.includes(term) ||
        (x.subProjectCode ?? '').includes(term) ||
        x.mainProjectName.includes(term),
    );
  });

  protected readonly kpiTotal = computed(() => this.items().length);
  protected readonly kpiDone = computed(
    () => this.items().filter((x) => x.completedStages === x.totalStages).length,
  );
  protected readonly kpiActive = computed(
    () => this.items().filter((x) => x.completedStages > 0 && x.completedStages < x.totalStages).length,
  );

  ngOnInit(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        const sorted = [...years].sort((a, b) => b.startDate.localeCompare(a.startDate));
        if (sorted.length > 0) {
          this.selectedYearId.set(sorted[0].id);
        }
        this.load();
      },
      error: () => this.load(),
    });
  }

  protected onYearChange(id: number | null): void {
    this.selectedYearId.set(id);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.financial.getSubProjects(this.selectedYearId()).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذر تحميل بيانات التعاقدات');
        this.loading.set(false);
      },
    });
  }

  protected progress(item: ProcurementSubProjectListItem): number {
    return item.totalStages === 0 ? 0 : Math.round((item.completedStages / item.totalStages) * 100);
  }
}
