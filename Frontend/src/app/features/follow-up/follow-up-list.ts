import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FollowUpService } from '../../core/services/follow-up.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import { ExecutionStage, FollowUpListItem } from '../../core/models/follow-up.models';
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
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

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

  protected readonly stages = signal<ExecutionStage[]>([]);
  protected readonly stagesLoading = signal(false);
  protected readonly showAddStage = signal(false);
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

  private loadStages(subProjectId: number): void {
    this.stagesLoading.set(true);
    this.followUp.getStages(subProjectId).subscribe({
      next: (stages) => {
        this.stages.set(stages);
        this.stagesLoading.set(false);
      },
      error: () => this.stagesLoading.set(false),
    });
  }

  protected openAddStage(): void {
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
    if (!item || this.savingStage()) return;

    if (!this.newStageName().trim() || !this.newStageDeadline()) {
      this.stageError.set('اسم المرحلة والموعد النهائي مطلوبان');
      return;
    }

    this.savingStage.set(true);
    this.stageError.set(null);

    this.followUp
      .createStage(item.subProjectId, {
        name: this.newStageName().trim(),
        deadline: this.newStageDeadline(),
        selfFundingSpent: this.newStageSelfSpent(),
        bankFundingSpent: this.newStageBankSpent(),
        physicalProgressPercent: this.newStageProgress(),
        notes: this.newStageNotes(),
        selfFundingProof: this.newStageSelfFile,
        bankFundingProof: this.newStageBankFile,
        physicalProgressProof: this.newStageProgressFile,
      })
      .subscribe({
        next: () => {
          this.savingStage.set(false);
          this.showAddStage.set(false);
          this.loadStages(item.subProjectId);
          this.load();
        },
        error: (err) => {
          this.savingStage.set(false);
          this.stageError.set(err?.error?.message ?? 'تعذّر حفظ المرحلة');
        },
      });
  }

  protected completeStage(stage: ExecutionStage): void {
    const item = this.selectedItem();
    if (!item) return;
    this.followUp.markComplete(item.subProjectId, stage.id).subscribe({
      next: () => this.loadStages(item.subProjectId),
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
    if (!item || this.savingPenalty()) return;
    this.savingPenalty.set(true);
    this.followUp
      .setPenalty(item.subProjectId, stage.id, this.penaltyAmountDraft(), this.penaltyPaidDraft())
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
    const label = key === 'self' ? 'ذاتي' : key === 'bank' ? 'بنكي' : 'تنفيذ-عيني';
    const realName =
      key === 'self'
        ? stage.selfFundingProofFileName
        : key === 'bank'
          ? stage.bankFundingProofFileName
          : stage.physicalProgressProofFileName;
    const fileName = realName && realName.trim() ? realName : `${stage.name}-${label}`;
    this.followUp
      .downloadFile(stage.subProjectId, stage.id, key)
      .subscribe((blob) => this.followUp.saveBlob(blob, fileName));
  }

  protected money(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString('en-US');
  }

  protected overdue(item: FollowUpListItem): boolean {
    return !!item.nextDeadline && new Date(item.nextDeadline) < new Date();
  }
}
