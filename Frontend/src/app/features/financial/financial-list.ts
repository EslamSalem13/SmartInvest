import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FinancialService } from '../../core/services/financial.service';
import { ProcurementSubProjectListItem } from '../../core/models/financial.models';

@Component({
  selector: 'app-financial-list',
  imports: [RouterLink, FormsModule],
  templateUrl: './financial-list.html',
  styleUrl: './financial.css',
})
export class FinancialList implements OnInit {
  private readonly financial = inject(FinancialService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<ProcurementSubProjectListItem[]>([]);
  protected readonly search = signal('');

  // ===== فلتر تقدم الطرح (اختيار متعدد) =====
  protected readonly stageFilter = signal<Set<number>>(new Set());
  protected readonly filterOpen = signal(false);

  /** أقصى عدد مراحل — يُشتق من البيانات (6 افتراضيًا). */
  protected readonly maxStages = computed(() => {
    const items = this.items();
    return items.length > 0 ? Math.max(...items.map((x) => x.totalStages)) : 6;
  });

  /** خيارات الفلتر: 0/6 حتى 6/6 مع عدد المشروعات في كل حالة. */
  protected readonly stageOptions = computed(() => {
    const max = this.maxStages();
    const items = this.items();

    return Array.from({ length: max + 1 }, (_, value) => ({
      value,
      label: `${value}/${max}`,
      isDone: value === max,
      count: items.filter((x) => x.completedStages === value).length,
    }));
  });

  protected readonly selectedStagesCount = computed(() => this.stageFilter().size);

  protected isStageChecked(value: number): boolean {
    return this.stageFilter().has(value);
  }

  protected toggleStage(value: number, checked: boolean): void {
    const next = new Set(this.stageFilter());
    if (checked) {
      next.add(value);
    } else {
      next.delete(value);
    }
    this.stageFilter.set(next);
  }

  protected clearStageFilter(): void {
    this.stageFilter.set(new Set());
  }

  protected toggleFilterPanel(): void {
    this.filterOpen.set(!this.filterOpen());
  }

  // ===== البحث والفلترة =====
  protected readonly filtered = computed(() => {
    const term = this.search().trim();
    const stages = this.stageFilter();

    return this.items().filter((x) => {
      const matchTerm =
        !term ||
        x.subProjectName.includes(term) ||
        (x.subProjectCode ?? '').includes(term) ||
        x.mainProjectName.includes(term);

      const matchStage = stages.size === 0 || stages.has(x.completedStages);

      return matchTerm && matchStage;
    });
  });

  // ===== ترقيم الصفحات (10 لكل صفحة — نفس صفحة المشروعات) =====
  protected readonly page = signal(1);
  protected readonly pageSize = 10;

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filtered().length / this.pageSize)),
  );

  protected readonly paged = computed(() => {
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

  protected readonly kpiTotal = computed(() => this.items().length);
  protected readonly kpiDone = computed(
    () => this.items().filter((x) => x.completedStages === x.totalStages).length,
  );
  protected readonly kpiActive = computed(
    () => this.items().filter((x) => x.completedStages > 0 && x.completedStages < x.totalStages).length,
  );

  constructor() {
    // أي تغيير في البحث أو الفلتر يرجّع للصفحة الأولى
    effect(() => {
      this.search();
      this.stageFilter();
      this.page.set(1);
    });
  }

  ngOnInit(): void {
    this.financial.getSubProjects().subscribe({
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
