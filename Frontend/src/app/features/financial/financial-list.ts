import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
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

  /** فلتر تقدم الطرح (0..6) — اختيار متعدد؛ مجموعة فارغة تعني بدون فلترة */
  protected readonly stageCountFilter = signal<Set<number>>(new Set());
  protected readonly stageCountOptions = [0, 1, 2, 3, 4, 5, 6];

  protected toggleStageCount(n: number): void {
    this.stageCountFilter.update((current) => {
      const next = new Set(current);
      if (next.has(n)) {
        next.delete(n);
      } else {
        next.add(n);
      }
      return next;
    });
  }

  protected readonly filtered = computed(() => {
    const term = this.search().trim();
    const stages = this.stageCountFilter();

    return this.items().filter((x) => {
      const matchesTerm =
        !term ||
        x.subProjectName.includes(term) ||
        (x.subProjectCode ?? '').includes(term) ||
        x.mainProjectName.includes(term);
      const matchesStages = stages.size === 0 || stages.has(x.completedStages);
      return matchesTerm && matchesStages;
    });
  });

  // ===== pagination =====
  protected readonly page = signal(1);
  protected readonly pageSize = 10;
  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filtered().length / this.pageSize)),
  );
  protected readonly pagedItems = computed<ProcurementSubProjectListItem[]>(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });
  protected readonly rangeStart = computed(() =>
    this.filtered().length === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly rangeEnd = computed(() =>
    Math.min(this.page() * this.pageSize, this.filtered().length),
  );

  protected goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.page.set(p);
    }
  }

  constructor() {
    effect(() => {
      this.search();
      this.stageCountFilter();
      this.page.set(1);
    });
  }

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
