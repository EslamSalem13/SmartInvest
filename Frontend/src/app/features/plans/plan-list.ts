import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { PlansService } from '../../core/services/plans.service';
import { ProjectsService } from '../../core/services/projects.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { FinancialYear, Plan } from '../../core/models/project.models';

@Component({
  selector: 'app-plan-list',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './plan-list.html',
  styleUrl: './plan-list.css',
})
export class PlanList {
  private readonly plansService = inject(PlansService);
  private readonly projectsService = inject(ProjectsService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly isManager = this.auth.isManager;

  // ===== السنة المالية =====
  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );
  protected readonly generating = signal(false);

  // ===== قائمة الخطط =====
  protected readonly plans = signal<Plan[]>([]);
  protected readonly plansLoading = signal(true);
  protected readonly plansError = signal<string | null>(null);
  protected readonly statusFilter = signal<'all' | 'approved' | 'pending'>('all');

  protected readonly filteredPlans = computed(() => {
    const filter = this.statusFilter();
    const yearId = this.selectedYearId();
    return [...this.plans()]
      .filter((p) => yearId == null || p.financialYearId === yearId)
      .filter((p) => {
        if (filter === 'approved') return p.planStatus === 'Approved';
        if (filter === 'pending') return p.planStatus !== 'Approved';
        return true;
      })
      .sort((a, b) => b.suggestionDate.localeCompare(a.suggestionDate));
  });

  // ===== توليد خطة جديدة (مقترحة أو معتمدة) =====
  protected readonly hasSuggestedForSelectedYear = computed(() => {
    const yearId = this.selectedYearId();
    return this.plans().some((p) => p.financialYearId === yearId && p.planStatus === 'Suggested');
  });

  protected readonly showAddPlanForm = signal(false);
  protected readonly newPlanType = signal<'Suggested' | 'Approved'>('Suggested');
  protected readonly newPlanApprovalDate = signal('');
  protected readonly addPlanError = signal<string | null>(null);
  protected readonly maxApprovalDate = computed(() => this.toLocalIsoDate(new Date()));

  constructor() {
    this.loadFinancialYears();
    this.loadPlans();
  }

  private loadFinancialYears(): void {
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        this.selectedYearId.set(
          this.financialYearsService.resolveSelectedYearId(years, this.selectedYearId()),
        );
      },
      error: () => {},
    });
  }

  protected onYearChange(id: number | null): void {
    this.selectedYearId.set(id);
    this.financialYearsService.rememberSelectedYearId(id);
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

  protected statusLabel(status: string): string {
    return status === 'Approved' ? 'معتمدة' : 'مقترحة';
  }

  private toLocalIsoDate(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  protected openAddPlan(): void {
    if (!this.selectedYearId() || this.generating()) return;
    this.addPlanError.set(null);
    this.newPlanType.set(
      this.hasSuggestedForSelectedYear() && this.isManager() ? 'Approved' : 'Suggested',
    );
    this.newPlanApprovalDate.set(this.maxApprovalDate());
    this.showAddPlanForm.set(true);
  }

  protected closeAddPlan(): void {
    this.showAddPlanForm.set(false);
  }

  protected confirmAddPlan(): void {
    if (this.generating()) return;
    this.addPlanError.set(null);

    const yearId = this.selectedYearId();
    if (!yearId) return;

    const type = this.newPlanType();

    if (type === 'Suggested' && this.hasSuggestedForSelectedYear()) {
      this.addPlanError.set('توجد بالفعل خطة مقترحة لهذه السنة المالية.');
      return;
    }

    let approvalDate: string | null = null;
    if (type === 'Approved') {
      approvalDate = this.newPlanApprovalDate();
      if (!approvalDate) {
        this.addPlanError.set('برجاء إدخال تاريخ الاعتماد.');
        return;
      }
      if (approvalDate > this.maxApprovalDate()) {
        this.addPlanError.set('لا يمكن أن يكون تاريخ الاعتماد في المستقبل.');
        return;
      }
    }

    this.showAddPlanForm.set(false);
    this.createPlan(yearId, type, approvalDate);
  }

  private createPlan(yearId: number, type: 'Suggested' | 'Approved', approvalDate: string | null): void {
    const year = this.financialYears().find((y) => y.id === yearId);
    if (!year) return;

    this.generating.set(true);
    this.projectsService.searchSubProjects({ financialYearId: yearId, page: 1, pageSize: 5000 }).subscribe({
      next: (result) => {
        const subProjectIds =
          type === 'Approved'
            ? result.items.filter((s) => s.isApproved).map((s) => s.id)
            : result.items.map((s) => s.id);

        if (type === 'Approved') {
          this.createOrApproveSuggestedPlan(
            yearId,
            year.name,
            year.startDate,
            year.endDate,
            approvalDate!,
            subProjectIds,
          );
          return;
        }

        this.plansService.create({
          planName: `الخطة المقترحة - ${year.name}`,
          startDate: year.startDate,
          endDate: year.endDate,
          planStatus: 'Suggested',
          approvalDate: null,
          financialYearId: yearId,
        }).subscribe({
          next: (plan) => this.addAllThenGo(plan.planId, subProjectIds),
          error: (err) => {
            this.generating.set(false);
            this.toast.error(err?.error?.message ?? 'تعذّر إنشاء الخطة');
          },
        });
      },
      error: () => {
        this.generating.set(false);
        this.toast.error('تعذّر تحميل مشروعات السنة المالية');
      },
    });
  }

  private createOrApproveSuggestedPlan(
    yearId: number,
    yearName: string,
    startDate: string,
    endDate: string,
    approvalDate: string,
    subProjectIds: number[],
  ): void {
    const existing = this.plans().find(
      (plan) => plan.financialYearId === yearId && plan.planStatus === 'Suggested',
    );
    const plan$ = existing
      ? of({ planId: existing.planId })
      : this.plansService.create({
          planName: `الخطة المقترحة - ${yearName}`,
          startDate,
          endDate,
          planStatus: 'Suggested',
          approvalDate: null,
          financialYearId: yearId,
        });

    plan$.pipe(
      switchMap((plan) => {
        const links$ = subProjectIds.length
          ? forkJoin(subProjectIds.map((id) => this.plansService.addExistingProject(plan.planId, id)))
          : of([]);
        return links$.pipe(
          switchMap(() => this.plansService.approve(plan.planId, { approvalDate })),
        );
      }),
    ).subscribe({
      next: (approvedPlan) => {
        this.generating.set(false);
        this.toast.success('تم اعتماد الخطة بنجاح، وتمت جدولة إشعارات الإدارة المالية للإرسال.');
        this.router.navigate(['/app/plans', approvedPlan.planId]);
      },
      error: (err) => {
        this.generating.set(false);
        this.toast.error(err?.error?.message ?? 'تعذّر اعتماد الخطة. لم يتم إرسال أي إشعار.');
      },
    });
  }

  private addAllThenGo(planId: number, subProjectIds: number[]): void {
    if (subProjectIds.length === 0) {
      this.generating.set(false);
      this.router.navigate(['/app/plans', planId]);
      return;
    }
    const calls = subProjectIds.map((id) => this.plansService.addExistingProject(planId, id));
    forkJoin(calls).subscribe({
      next: () => {
        this.generating.set(false);
        this.router.navigate(['/app/plans', planId]);
      },
      error: () => {
        this.generating.set(false);
        this.toast.error('تعذّر إضافة بعض المشروعات للخطة، قد تكون الخطة المطبوعة غير مكتملة');
        this.router.navigate(['/app/plans', planId]);
      },
    });
  }
}
