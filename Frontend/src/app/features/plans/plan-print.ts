import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PlansService } from '../../core/services/plans.service';
import { PlanDetail } from '../../core/models/project.models';
import { formatEgpAsThousands } from '../../core/utils/budget.util';

@Component({
  selector: 'app-plan-print',
  imports: [RouterLink, DatePipe],
  templateUrl: './plan-print.html',
  styleUrl: './plan-print.css',
})
export class PlanPrint {
  private readonly route = inject(ActivatedRoute);
  private readonly plansService = inject(PlansService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly plan = signal<PlanDetail | null>(null);

  protected readonly totalCost = computed(
    () => this.plan()?.projects?.reduce((a, p) => a + p.totalCost, 0) ?? 0,
  );

  constructor() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.plansService.getById(id).subscribe({
      next: (p) => {
        this.plan.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل الخطة');
        this.loading.set(false);
      },
    });
  }

  protected thousandsLabel(value: number): string {
    return formatEgpAsThousands(value);
  }

  protected statusLabel(status: string): string {
    return status === 'Approved' ? 'معتمدة' : 'مقترحة';
  }

  protected print(): void {
    window.print();
  }
}
