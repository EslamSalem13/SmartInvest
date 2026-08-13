import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FollowUpService } from '../../core/services/follow-up.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import { FinancialService } from '../../core/services/financial.service';
import { ToastService } from '../../core/services/toast.service';
import { ExecutionStage, FollowUpListItem } from '../../core/models/follow-up.models';
import { FinancialYear } from '../../core/models/project.models';
import { formatEgpAsThousands } from '../../core/utils/budget.util';

@Component({
  selector: 'app-follow-up-list',
  imports: [FormsModule, DatePipe],
  templateUrl: './follow-up-list.html',
  styleUrl: './follow-up-list.css',
})
export class FollowUpList implements OnInit {
  private readonly followUp = inject(FollowUpService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);
  private readonly financial = inject(FinancialService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isManager = computed(() =>
    this.auth.isPlanningManager() || this.auth.isFinancialManager() || this.auth.isSuperAdmin(),
  );

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
    const yearIdRaw = this.route.snapshot.queryParamMap.get('financialYearId');
    const yearId = yearIdRaw != null ? Number(yearIdRaw) : NaN;
    if (!Number.isNaN(yearId)) {
      this.selectedYearId.set(yearId);
    }

    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        this.selectedYearId.set(
          this.financialYearsService.resolveSelectedYearId(years, this.selectedYearId()),
        );
        this.load();
      },
      error: () => this.load(),
    });
  }

  protected onYearChange(id: number | null): void {
    this.selectedYearId.set(id);
    this.financialYearsService.rememberSelectedYearId(id);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { financialYearId: id },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
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

  protected readonly stages = signal<ExecutionStage[]>([]);
  protected readonly stagesLoading = signal(false);
  protected readonly showAddStage = signal(false);
  protected readonly editingStage = signal<ExecutionStage | null>(null);
  protected readonly savingStage = signal(false);
  protected readonly stageError = signal<string | null>(null);

  protected readonly newStageName = signal('');
  protected readonly newStageDeadline = signal('');
  protected readonly newStageSelfSpent = signal(0);
  protected readonly newStageBankSpent = signal(0);
  protected readonly newStageProgress = signal(0);
  protected readonly newStageNotes = signal('');
  protected newStageSelfFile: File | null = null;
  protected newStageBankFile: File | null = null;
  protected newStageProgressFile: File | null = null;

  protected onSelectStages(item: FollowUpListItem): void {
    this.openStages(item);
    this.loadStages(item.subProjectId);
  }

  protected loadStages(subProjectId: number): void {
    const financialYearId = this.selectedYearId();
    if (financialYearId == null) return;
    this.stagesLoading.set(true);
    this.followUp.getStages(subProjectId, financialYearId).subscribe({
      next: (stages) => {
        this.stages.set(stages);
        this.stagesLoading.set(false);
      },
      error: () => this.stagesLoading.set(false),
    });
  }

  protected readonly showHandover = signal(false);
  protected readonly handoverDate = signal('');
  protected readonly handoverSaving = signal(false);
  protected handoverFile: File | null = null;

  /** مرحلة التسليم الأولي بلا موعد = الأرضية لم تُسلَّم بعد */
  protected readonly awaitingHandover = computed(() =>
    this.stages().some((s) => s.isFinalDelivery && s.deadline == null),
  );

  /** مرحلة التسليم الأولي بموعد محسوب = تم تسجيل تسليم سابقًا، فالإجراء المتاح الآن تصحيحه */
  protected readonly canCorrectHandover = computed(() =>
    this.stages().some((s) => s.isFinalDelivery && s.deadline != null),
  );

  protected openHandover(): void {
    this.handoverDate.set('');
    this.handoverFile = null;
    this.showHandover.set(true);
  }

  protected closeHandover(): void {
    this.showHandover.set(false);
  }

  protected onHandoverFileChange(event: Event): void {
    this.handoverFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected saveHandover(): void {
    const item = this.selectedItem();
    if (!item || this.handoverSaving()) {
      return;
    }
    if (!this.handoverDate()) {
      this.toast.error('برجاء تحديد تاريخ تسليم الأرضية');
      return;
    }
    if (!this.handoverFile) {
      this.toast.error('برجاء رفع إثبات تسليم الأرضية');
      return;
    }

    this.handoverSaving.set(true);
    this.financial.setSiteHandover(item.subProjectId, this.handoverDate(), this.handoverFile).subscribe({
      next: () => {
        this.handoverSaving.set(false);
        this.showHandover.set(false);
        this.toast.success('تم تسجيل تسليم الأرضية — تم احتساب الموعد النهائي');
        this.loadStages(item.subProjectId);
        this.load();
      },
      error: (err) => {
        this.handoverSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر تسجيل تسليم الأرضية');
      },
    });
  }

  protected openAddStage(): void {
    this.editingStage.set(null);
    this.newStageName.set('');
    this.newStageDeadline.set('');
    this.newStageSelfSpent.set(0);
    this.newStageBankSpent.set(0);
    this.newStageProgress.set(0);
    this.newStageNotes.set('');
    this.newStageSelfFile = null;
    this.newStageBankFile = null;
    this.newStageProgressFile = null;
    this.stageError.set(null);
    this.showAddStage.set(true);
  }

  protected closeAddStage(): void {
    this.showAddStage.set(false);
    this.editingStage.set(null);
  }

  protected openEditStage(stage: ExecutionStage): void {
    if (stage.isCompleted) {
      this.toast.error('يجب إعادة فتح المرحلة المكتملة قبل تعديلها');
      return;
    }
    this.editingStage.set(stage);
    this.newStageName.set(stage.name);
    this.newStageDeadline.set(stage.deadline?.slice(0, 10) ?? '');
    this.newStageSelfSpent.set(stage.selfFundingSpent);
    this.newStageBankSpent.set(stage.bankFundingSpent);
    this.newStageProgress.set(stage.physicalProgressPercent);
    this.newStageNotes.set(stage.notes ?? '');
    this.newStageSelfFile = null;
    this.newStageBankFile = null;
    this.newStageProgressFile = null;
    this.stageError.set(null);
    this.showAddStage.set(true);
  }

  protected onSelfFileChange(event: Event): void {
    this.newStageSelfFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected onBankFileChange(event: Event): void {
    this.newStageBankFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected onProgressFileChange(event: Event): void {
    this.newStageProgressFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected saveNewStage(): void {
    const item = this.selectedItem();
    const financialYearId = this.selectedYearId();
    if (!item || financialYearId == null || this.savingStage()) return;

    const editing = this.editingStage();
    if ((!editing?.isFinalDelivery && !this.newStageName().trim()) || (!editing?.isFinalDelivery && !this.newStageDeadline())) {
      this.stageError.set('اسم المرحلة والموعد النهائي مطلوبان');
      return;
    }

    this.savingStage.set(true);
    this.stageError.set(null);

    const payload = {
        name: this.newStageName().trim(),
        deadline: this.newStageDeadline(),
        selfFundingSpent: this.newStageSelfSpent(),
        bankFundingSpent: this.newStageBankSpent(),
        physicalProgressPercent: this.newStageProgress(),
        notes: this.newStageNotes(),
        selfFundingProof: this.newStageSelfFile,
        bankFundingProof: this.newStageBankFile,
        physicalProgressProof: this.newStageProgressFile,
      };
    const request = editing
      ? this.followUp.updateStage(item.subProjectId, editing.id, financialYearId, payload)
      : this.followUp.createStage(item.subProjectId, financialYearId, payload);

    request
      .subscribe({
        next: () => {
          this.savingStage.set(false);
          this.showAddStage.set(false);
          this.editingStage.set(null);
          this.toast.success(editing ? 'تم تعديل مرحلة التنفيذ' : 'تمت إضافة مرحلة التنفيذ');
          this.loadStages(item.subProjectId);
          this.load();
        },
        error: (err) => {
          this.savingStage.set(false);
          this.stageError.set(err?.error?.message ?? 'تعذّر حفظ المرحلة');
        },
      });
  }

  protected readonly completionCandidate = signal<FollowUpListItem | null>(null);
  protected readonly completingProjectId = signal<number | null>(null);
  protected readonly checkingCompletionId = signal<number | null>(null);

  protected completionHint(item: FollowUpListItem): string {
    return item.completionEligibility.blockers.join('\n') || 'كل شروط إكمال المشروع متحققة';
  }

  protected openCompletion(item: FollowUpListItem): void {
    const financialYearId = this.selectedYearId();
    if (!item.completionEligibility.canCompleteProject || financialYearId == null || this.checkingCompletionId() != null) return;
    this.checkingCompletionId.set(item.subProjectId);
    this.followUp.getCompletionEligibility(item.subProjectId, financialYearId).subscribe({
      next: (eligibility) => {
        this.checkingCompletionId.set(null);
        if (!eligibility.canCompleteProject) {
          this.toast.error(eligibility.blockers.join(' '));
          this.load();
          return;
        }
        this.completionCandidate.set({ ...item, completionEligibility: eligibility });
      },
      error: (err) => {
        this.checkingCompletionId.set(null);
        this.toast.error(err?.error?.message ?? 'تعذر التحقق من شروط إكمال المشروع');
      },
    });
  }

  protected closeCompletion(): void {
    if (this.completingProjectId() == null) this.completionCandidate.set(null);
  }

  protected confirmCompletion(): void {
    const item = this.completionCandidate();
    const financialYearId = this.selectedYearId();
    if (!item || financialYearId == null || this.completingProjectId() != null) return;

    this.completingProjectId.set(item.subProjectId);
    this.followUp.completeExecution(item.subProjectId, financialYearId).subscribe({
      next: () => {
        this.completingProjectId.set(null);
        this.completionCandidate.set(null);
        this.toast.success('تم إكمال المشروع وتغيير حالته إلى منتهي');
        this.closeStages();
        this.load();
      },
      error: (err) => {
        this.completingProjectId.set(null);
        this.toast.error(err?.error?.message ?? 'تعذر إكمال المشروع');
        this.load();
      },
    });
  }

  protected completeStage(stage: ExecutionStage): void {
    const item = this.selectedItem();
    const financialYearId = this.selectedYearId();
    if (!item || financialYearId == null) return;
    this.followUp.markComplete(item.subProjectId, stage.id, financialYearId).subscribe({
      next: () => {
        this.toast.success('تم إنهاء المرحلة');
        this.loadStages(item.subProjectId);
        this.load();
      },
      error: (err) => this.toast.error(err?.error?.message ?? 'تعذر إنهاء المرحلة'),
    });
  }

  /** عكس إنهاء مرحلة عن طريق الخطأ — مدير التخطيط فقط */
  protected reopenStage(stage: ExecutionStage): void {
    const item = this.selectedItem();
    const financialYearId = this.selectedYearId();
    if (!item || financialYearId == null) return;
    this.followUp.reopenStage(item.subProjectId, stage.id, financialYearId).subscribe({
      next: () => {
        this.toast.success('تم إعادة فتح المرحلة');
        this.loadStages(item.subProjectId);
        this.load();
      },
      error: (err) => this.toast.error(err?.error?.message ?? 'تعذر إعادة فتح المرحلة'),
    });
  }

  protected readonly editingPenaltyStageId = signal<number | null>(null);
  protected readonly penaltyAmountDraft = signal<number | null>(null);
  protected readonly penaltyPaidDraft = signal(false);
  protected readonly savingPenalty = signal(false);

  protected startEditPenalty(stage: ExecutionStage): void {
    this.editingPenaltyStageId.set(stage.id);
    this.penaltyAmountDraft.set(stage.penaltyAmount);
    this.penaltyPaidDraft.set(stage.penaltyPaid);
  }

  protected cancelEditPenalty(): void {
    this.editingPenaltyStageId.set(null);
  }

  protected updatePenaltyAmountDraft(value: number | string): void {
    this.penaltyAmountDraft.set(value === '' || value == null ? null : Number(value));
  }

  protected savePenalty(stage: ExecutionStage): void {
    const item = this.selectedItem();
    const financialYearId = this.selectedYearId();
    if (!item || financialYearId == null || this.savingPenalty()) return;
    this.savingPenalty.set(true);
    this.followUp
      .setPenalty(item.subProjectId, stage.id, financialYearId, this.penaltyAmountDraft(), this.penaltyPaidDraft())
      .subscribe({
        next: () => {
          this.savingPenalty.set(false);
          this.editingPenaltyStageId.set(null);
          this.loadStages(item.subProjectId);
        },
        error: () => {
          this.savingPenalty.set(false);
        },
      });
  }

  /**
   * رابط <a href> مباشر لملف موثّق بـ [Authorize] يفشل بـ 401 عند النقر — التنقل الطبيعي
   * للمتصفح لا يمر على auth.interceptor فلا يُرسل رأس Authorization. نجلب الملف كـ Blob
   * عبر HttpClient (نفس نمط FinancialService.downloadStageFile/saveBlob) بدل رابط مباشر.
   */
  protected downloadProof(stage: ExecutionStage, key: 'self' | 'bank' | 'progress'): void {
    const financialYearId = this.selectedYearId();
    if (financialYearId == null) return;
    const label = key === 'self' ? 'ذاتي' : key === 'bank' ? 'بنكي' : 'تنفيذ-عيني';
    const realName =
      key === 'self'
        ? stage.selfFundingProofFileName
        : key === 'bank'
          ? stage.bankFundingProofFileName
          : stage.physicalProgressProofFileName;
    const fileName = realName && realName.trim() ? realName : `${stage.name}-${label}`;
    this.followUp
      .downloadFile(stage.subProjectId, stage.id, financialYearId, key)
      .subscribe((blob) => this.followUp.saveBlob(blob, fileName));
  }

  protected thousandsLabel(value: number | null | undefined): string {
    return formatEgpAsThousands(value);
  }

  protected overdue(item: FollowUpListItem): boolean {
    return !!item.nextDeadline && new Date(item.nextDeadline) < new Date();
  }
}
