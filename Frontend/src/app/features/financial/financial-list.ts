import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
