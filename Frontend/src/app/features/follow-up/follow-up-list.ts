import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FollowUpService } from '../../core/services/follow-up.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { FollowUpListItem } from '../../core/models/follow-up.models';
import { FinancialYear } from '../../core/models/project.models';

@Component({
  selector: 'app-follow-up-list',
  imports: [FormsModule, DatePipe],
  templateUrl: './follow-up-list.html',
  styleUrl: './follow-up-list.css',
})
export class FollowUpList implements OnInit {
  private readonly followUp = inject(FollowUpService);
  private readonly financialYearsService = inject(FinancialYearsService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<FollowUpListItem[]>([]);
  protected readonly search = signal('');

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  protected readonly filtered = computed(() => {
    const term = this.search().trim();
    if (!term) return this.items();
    return this.items().filter(
      (x) =>
        x.subProjectName.includes(term) ||
        (x.subProjectCode ?? '').includes(term) ||
        x.mainProjectName.includes(term),
    );
  });

  protected readonly kpiTotal = computed(() => this.items().length);
  protected readonly kpiStalled = computed(() => this.items().filter((x) => x.isStalled).length);
  protected readonly kpiOverdue = computed(
    () => this.items().filter((x) => x.nextDeadline && new Date(x.nextDeadline) < new Date()).length,
  );

  protected readonly selectedItem = signal<FollowUpListItem | null>(null);

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

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.followUp.getList({ financialYearId: this.selectedYearId() }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذر تحميل بيانات متابعة المشروعات');
        this.loading.set(false);
      },
    });
  }

  protected openStages(item: FollowUpListItem): void {
    this.selectedItem.set(item);
  }

  protected closeStages(): void {
    this.selectedItem.set(null);
  }

  protected overdue(item: FollowUpListItem): boolean {
    return !!item.nextDeadline && new Date(item.nextDeadline) < new Date();
  }
}
