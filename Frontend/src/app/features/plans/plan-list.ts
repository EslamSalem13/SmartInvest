import { Component, computed, inject, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { PlansService } from '../../core/services/plans.service';
import { ProjectsService } from '../../core/services/projects.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import { FinancialYear, Plan } from '../../core/models/project.models';

@Component({
  selector: 'app-plan-list',
  imports: [FormsModule, RouterLink, SlicePipe],
  templateUrl: './plan-list.html',
  styleUrl: './plan-list.css',
})
export class PlanList {
  private readonly plansService = inject(PlansService);
  private readonly projectsService = inject(ProjectsService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly isManager = this.auth.isManager;

  // ===== السنة المالية =====
  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );
  protected readonly generating = signal(false);

  protected readonly showApprovedDateForm = signal(false);
  protected readonly approvedDate = signal('');

  // ===== قائمة الخطط =====
  protected readonly plans = signal<Plan[]>([]);
  protected readonly plansLoading = signal(true);
  protected readonly plansError = signal<string | null>(null);
  protected readonly statusFilter = signal<'all' | 'approved' | 'pending'>('all');

  protected readonly filteredPlans = computed(() => {
    const filter = this.statusFilter();
    return [...this.plans()]
      .filter((p) => {
        if (filter === 'approved') return p.planStatus === 'Approved';
        if (filter === 'pending') return p.planStatus !== 'Approved';
        return true;
      })
      .sort((a, b) => b.suggestionDate.localeCompare(a.suggestionDate));
  });

  constructor() {
    this.loadFinancialYears();
    this.loadPlans();
  }

  private loadFinancialYears(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        const sorted = [...years].sort((a, b) => b.startDate.localeCompare(a.startDate));
        if (this.selectedYearId() == null && sorted.length > 0) {
          this.selectedYearId.set(sorted[0].id);
        }
      },
    });
  }

  protected loadPlans(): void {
    this.plansLoading.set(true);
    this.plansError.set(null);
    this.plansService.getAll().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.plansLoading.set(false);
      },
      error: () => {
        this.plansError.set('تعذّر تحميل الخطط. تأكد من تشغيل الخادم وتسجيل الدخول.');
        this.plansLoading.set(false);
      },
    });
  }

  protected money(value: number): string {
    return (value ?? 0).toLocaleString('en-US');
  }

  protected statusLabel(status: string): string {
    return status === 'Approved' ? 'معتمدة' : 'مقترحة';
  }

  private toLocalIsoDate(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  // ===== إنشاء خطة مقترحة جديدة =====
  protected generateSuggested(): void {
    const yearId = this.selectedYearId();
    if (!yearId || this.generating()) return;
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.generating.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        this.plansService
          .create({
            planName: `الخطة المقترحة - ${year.name}`,
            startDate: year.startDate,
            endDate: year.endDate,
            planStatus: 'Suggested',
            financialYearId: yearId,
          })
          .subscribe({
            next: (plan) => this.addAllThenGo(plan.planId, result.items.map((s) => s.id)),
            error: () => {
              this.generating.set(false);
              alert('تعذّر إنشاء الخطة');
            },
          });
      },
      error: () => {
        this.generating.set(false);
        alert('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private addAllThenGo(planId: number, subProjectIds: number[]): void {
    if (subProjectIds.length === 0) {
      this.generating.set(false);
      this.loadPlans();
      this.router.navigate(['/app/plans', planId]);
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addExistingProject(planId, id));
    forkJoin(calls).subscribe({
      next: () => {
        this.generating.set(false);
        this.loadPlans();
        this.router.navigate(['/app/plans', planId]);
      },
      error: () => {
        this.generating.set(false);
        this.loadPlans();
        alert('تعذّر إضافة بعض المشروعات للخطة، قد تكون الخطة المطبوعة غير مكتملة');
        this.router.navigate(['/app/plans', planId]);
      },
    });
  }

  // ===== إنشاء خطة معتمدة جديدة =====
  protected openApprovedGenerate(): void {
    if (!this.selectedYearId()) return;
    this.approvedDate.set(this.toLocalIsoDate(new Date()));
    this.showApprovedDateForm.set(true);
  }

  protected closeApprovedGenerate(): void {
    this.showApprovedDateForm.set(false);
  }

  protected confirmApprovedGenerate(): void {
    const yearId = this.selectedYearId();
    const date = this.approvedDate();
    if (!yearId || !date || this.generating()) return;
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.showApprovedDateForm.set(false);
    this.generating.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        const approvedIds = result.items.filter((s) => s.isApproved).map((s) => s.id);
        this.plansService
          .create({
            planName: `الخطة المعتمدة - ${year.name}`,
            startDate: year.startDate,
            endDate: year.endDate,
            planStatus: 'Suggested',
            financialYearId: yearId,
          })
          .subscribe({
            next: (plan) => this.addAllThenApprove(plan.planId, approvedIds, date),
            error: () => {
              this.generating.set(false);
              alert('تعذّر إنشاء الخطة');
            },
          });
      },
      error: () => {
        this.generating.set(false);
        alert('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private addAllThenApprove(planId: number, subProjectIds: number[], approvalDate: string): void {
    const afterAdd = (addFailed: boolean) => {
      if (addFailed) {
        alert('تعذّر إضافة بعض المشروعات للخطة، قد تكون الخطة المطبوعة غير مكتملة');
      }
      this.plansService.approve(planId, { approvalDate }).subscribe({
        next: () => {
          this.generating.set(false);
          this.loadPlans();
          this.router.navigate(['/app/plans', planId]);
        },
        error: () => {
          this.generating.set(false);
          this.loadPlans();
          alert('تعذّر اعتماد الخطة، ستُطبع كخطة غير معتمدة');
          this.router.navigate(['/app/plans', planId]);
        },
      });
    };

    if (subProjectIds.length === 0) {
      afterAdd(false);
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addExistingProject(planId, id));
    forkJoin(calls).subscribe({ next: () => afterAdd(false), error: () => afterAdd(true) });
  }
}
