import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { FinancialService } from '../../core/services/financial.service';
import { ToastService } from '../../core/services/toast.service';
import { ContractorsService } from '../../core/services/contractors.service';
import { Contractor } from '../../core/models/project.models';
import { egpToThousands, formatEgpAsThousands, thousandsToEgp } from '../../core/utils/budget.util';
import {
  ContractAwardDetails,
  ProcurementOverview,
  ProcurementStage,
  ProcurementStageDetail,
  ProcurementVersion,
} from '../../core/models/financial.models';

@Component({
  selector: 'app-procurement-workflow',
  imports: [RouterLink, FormsModule],
  templateUrl: './procurement-workflow.html',
  styleUrl: './financial.css',
})
export class ProcurementWorkflow implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly financial = inject(FinancialService);
  private readonly contractorsService = inject(ContractorsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  protected readonly subProjectId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly overview = signal<ProcurementOverview | null>(null);

  /** المرحلة المفتوحة حاليًا + تفاصيلها (الإصدارات) */
  protected readonly openStage = signal<string | null>(null);
  protected readonly stageDetail = signal<ProcurementStageDetail | null>(null);
  protected readonly stageLoading = signal(false);

  /** نموذج رفع إصدار */
  protected readonly showUpload = signal(false);
  protected readonly uploadNotes = signal('');
  protected readonly uploadFiles = signal<Record<string, File>>({});
  protected readonly uploading = signal(false);
  protected readonly uploadError = signal<string | null>(null);
  /** مفتاح الخانة التي يُسحب ملف فوقها حاليًا (لإبراز منطقة الإفلات) */
  protected readonly dragOverKey = signal<string | null>(null);

  protected readonly busy = signal(false);

  protected readonly isStaff = this.auth.canEditFinancial;
  protected readonly isManager = this.auth.canManageFinancial;
  protected readonly canManageStageDuration = this.auth.canManageProcurementDuration;

  protected readonly completedCount = computed(
    () => this.overview()?.stages.filter((s) => s.isCompleted).length ?? 0,
  );

  // ===== مرحلة الترسية =====
  protected readonly contractors = signal<Contractor[]>([]);

  protected readonly award = computed(() => this.stageDetail()?.contractAward ?? null);

  /** نموذج الترسية — إشارة لكل حقل، على نمط باقي النماذج في المشروع */
  protected readonly aContractorId = signal<number | null>(null);
  protected readonly aContractDate = signal<string>('');
  protected readonly aContractValue = signal<number | null>(null);
  protected readonly aContractValueRaw = computed(() => thousandsToEgp(this.aContractValue()));
  protected readonly aAdvanceDone = signal(false);
  protected readonly aAdvancePercentage = signal<number | null>(null);
  protected readonly aAdvanceSelf = signal<number | null>(null);
  protected readonly aAdvanceSelfRaw = computed(() => thousandsToEgp(this.aAdvanceSelf()));
  protected readonly aAdvanceDate = signal('');
  protected readonly aAdvanceBank = signal<number | null>(null);
  protected readonly aAdvanceBankRaw = computed(() => thousandsToEgp(this.aAdvanceBank()));
  protected readonly aDurationMonths = signal<number | null>(null);
  protected readonly aDurationDays = signal<number | null>(null);
  protected readonly aHandoverMode = signal<number | null>(null);
  protected readonly aPenaltyAmount = signal<number | null>(null);
  protected readonly awardSaving = signal(false);
  protected readonly awardError = signal<string | null>(null);

  protected readonly aHandoverDate = signal<string>('');
  protected readonly aHandoverSaving = signal(false);
  protected aHandoverFile: File | null = null;

  protected aAdvanceProofFile: File | null = null;
  protected readonly aAdvanceProofUploading = signal(false);
  protected readonly aAdvanceProofError = signal<string | null>(null);

  /** قيمة الدفعة المقدمة بالجنيه — تُحسب من قيمة العقد لا الإجمالي المخطط، تظهر تلقائيًا بمجرد كتابة النسبة أو قيمة العقد */
  protected readonly advanceAmount = computed(() => {
    const pct = this.aAdvancePercentage();
    const base = this.aContractValueRaw();
    if (pct == null || pct <= 0 || base <= 0) {
      return 0;
    }
    return Math.round(base * pct) / 100;
  });

  /** ما تبقّى ليتوازن التقسيم بين الذاتي والبنكي */
  protected readonly advanceRemaining = computed(
    () => Math.round((this.advanceAmount() - this.aAdvanceSelfRaw() - this.aAdvanceBankRaw()) * 100) / 100,
  );

  /** تجاوز حد التمويل المخطط للمشروع — تحذير فوري في الواجهة؛ الفحص الملزم الفعلي يعيش في الخادم (BusinessRuleException) */
  protected readonly aAdvanceSelfExceeds = computed(
    () => this.aAdvanceSelfRaw() > (this.award()?.selfFunding ?? 0),
  );
  protected readonly aAdvanceBankExceeds = computed(
    () => this.aAdvanceBankRaw() > (this.award()?.bankFunding ?? 0),
  );

  protected readonly awardEditable = computed(
    () => this.isStaff() && !this.stageDetail()?.isCompleted,
  );

  /**
   * ملف المقاول المختار (الغرامات + هل نتعامل معه تاني + آخر ملاحظة).
   * لا يُشتق من contractors() لأن /api/contractors (القائمة) لا يحسب totalFines/unpaidFines/notes —
   * هذه الحقول تُحسب فقط في GetByIdAsync، لذا نجلبها مباشرة عند تغيّر المقاول المختار.
   */
  protected readonly selectedContractorProfile = signal<Contractor | null>(null);

  constructor() {
    effect(() => {
      const id = this.aContractorId();
      if (id == null) {
        this.selectedContractorProfile.set(null);
        return;
      }
      this.contractorsService.getById(id).subscribe({
        next: (full) => this.selectedContractorProfile.set(full),
        error: () => this.selectedContractorProfile.set(null),
      });
    });

    /** المصدر الوحيد لمسح ملف/خطأ إثبات الدفعة المقدمة المُجهَّز — يتفاعل مع aAdvanceDone أيًا كان مصدر
     * تغييرها (تأشير يدوي عبر onAdvanceDoneChange أو مزامنة من الخادم عبر syncAwardForm بعد reload())،
     * حتى لا يبقى ملف قديم محتفَظًا به خلف مربّع اختيار يبدو فارغًا. */
    effect(() => {
      if (!this.aAdvanceDone()) {
        this.aAdvanceProofFile = null;
        this.aAdvanceProofError.set(null);
      }
    });
  }

  ngOnInit(): void {
    this.reload();
  }

  private loadAwardLookups(): void {
    if (this.contractors().length > 0) {
      return;
    }
    this.contractors.set([]);
    this.contractorsService.getAll().subscribe({
      next: (items) => this.contractors.set(items.filter((c) => c.isActive)),
    });
  }

  /** يملأ النموذج من الخادم — يُستدعى كلما أُعيد تحميل تفاصيل المرحلة */
  private syncAwardForm(details: ContractAwardDetails | null | undefined): void {
    this.awardError.set(null);
    if (!details) {
      return;
    }
    this.aContractorId.set(details.contractorId);
    this.aContractDate.set(details.contractDate?.slice(0, 10) ?? '');
    this.aContractValue.set(egpToThousands(details.contractValue));
    this.aAdvanceDone.set(details.advancePaymentDone);
    this.aAdvancePercentage.set(details.advancePaymentPercentage);
    this.aAdvanceSelf.set(egpToThousands(details.advancePaymentSelfAmount));
    this.aAdvanceBank.set(egpToThousands(details.advancePaymentBankAmount));
    this.aAdvanceDate.set(details.advancePaymentDate?.slice(0, 10) ?? '');
    this.aDurationMonths.set(details.executionDurationMonths);
    this.aDurationDays.set(details.executionDurationDays);
    this.aHandoverMode.set(details.siteHandoverMode);
    this.aHandoverDate.set(details.siteHandoverDate?.slice(0, 10) ?? '');
    this.aPenaltyAmount.set(details.penaltyAmount);
  }

  protected saveAward(): void {
    if (this.awardSaving()) {
      return;
    }
    this.awardSaving.set(true);
    this.awardError.set(null);
    this.financial
      .setContractAwardDetails(this.subProjectId, {
        advancePaymentDone: this.aAdvanceDone(),
        advancePaymentPercentage: this.aAdvancePercentage(),
        advancePaymentSelfAmount: this.aAdvanceSelfRaw(),
        advancePaymentBankAmount: this.aAdvanceBankRaw(),
        advancePaymentDate: this.aAdvanceDate() || null,
        executionDurationMonths: this.aDurationMonths(),
        executionDurationDays: this.aDurationDays(),
        siteHandoverMode: this.aHandoverMode(),
        penaltyAmount: this.aPenaltyAmount(),
        contractorId: this.aContractorId(),
        contractDate: this.aContractDate() || null,
        // فارغ يُرسَل null لا صفر — صفر يجعل الخادم يحسب «وفرة» مزيّفة تساوي كامل الميزانية المخططة (totalCost - 0)
        contractValue: this.aContractValue() == null ? null : this.aContractValueRaw(),
      })
      .subscribe({
        next: () => this.saveAwardThenHandoverIfStaged(),
        error: (err) => {
          this.awardSaving.set(false);
          this.awardError.set(err?.error?.message ?? 'تعذر حفظ بيانات الترسية');
        },
      });
  }

  /** بعد نجاح حفظ بيانات الترسية: لو المستخدم اختار "مُسلَّمة للمقاول" وجهّز تاريخًا وملفًا، يُسجَّل التسليم في نفس الحفظة — لا حاجة لخطوة حفظ منفصلة. */
  private saveAwardThenHandoverIfStaged(): void {
    if (this.aHandoverMode() !== 1 || !this.aHandoverDate() || !this.aHandoverFile) {
      this.awardSaving.set(false);
      this.toast.success('تم حفظ بيانات الترسية');
      this.reload();
      return;
    }

    this.financial.setSiteHandover(this.subProjectId, this.aHandoverDate(), this.aHandoverFile).subscribe({
      next: () => {
        this.awardSaving.set(false);
        this.aHandoverFile = null;
        this.toast.success('تم حفظ بيانات الترسية وتسجيل تسليم الأرضية');
        this.reload();
      },
      error: (err) => {
        this.awardSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تم حفظ بيانات الترسية، لكن تعذر تسجيل تسليم الأرضية');
        this.reload();
      },
    });
  }

  protected onAdvanceProofFileChange(event: Event): void {
    this.aAdvanceProofFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected onAdvanceDoneChange(checked: boolean): void {
    this.aAdvanceDone.set(checked);
  }

  protected uploadAdvanceProof(): void {
    if (this.aAdvanceProofUploading() || !this.aAdvanceProofFile) {
      return;
    }
    this.aAdvanceProofUploading.set(true);
    this.aAdvanceProofError.set(null);
    this.financial
      .setAdvancePaymentProof(this.subProjectId, this.aAdvanceProofFile)
      .subscribe({
        next: () => {
          this.aAdvanceProofUploading.set(false);
          this.aAdvanceProofFile = null;
          this.toast.success('تم رفع إثبات صرف الدفعة المقدمة');
          this.reload();
        },
        error: (err) => {
          this.aAdvanceProofUploading.set(false);
          this.aAdvanceProofError.set(err?.error?.message ?? 'تعذر رفع الملف');
        },
      });
  }

  protected onHandoverFileChange(event: Event): void {
    this.aHandoverFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected saveHandover(): void {
    if (this.aHandoverSaving()) {
      return;
    }
    if (!this.aHandoverDate()) {
      this.toast.error('برجاء تحديد تاريخ تسليم الأرضية');
      return;
    }
    if (!this.aHandoverFile) {
      this.toast.error('برجاء رفع إثبات تسليم الأرضية');
      return;
    }

    this.aHandoverSaving.set(true);
    this.financial.setSiteHandover(this.subProjectId, this.aHandoverDate(), this.aHandoverFile).subscribe({
      next: () => {
        this.aHandoverSaving.set(false);
        this.aHandoverFile = null;
        this.toast.success('تم تسجيل تسليم الأرضية');
        this.reload();
      },
      error: (err) => {
        this.aHandoverSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر تسجيل تسليم الأرضية');
      },
    });
  }

  protected downloadHandoverProof(name: string): void {
    this.financial.downloadSiteHandoverProof(this.subProjectId).subscribe({
      next: (blob) => this.financial.saveBlob(blob, name),
      error: () => this.toast.error('تعذر تنزيل إثبات تسليم الأرضية'),
    });
  }

  protected thousandsLabel(value: number | null | undefined): string {
    return formatEgpAsThousands(value);
  }

  // ===== المدة القصوى (كل المراحل عدا الإعلان والترسية — للترسية مدة تنفيذ خاصة بها) =====
  protected readonly durationInput = signal<number | null>(null);
  protected readonly durationSaving = signal(false);
  /** بعد تحديد المدة تُقفل للقراءة فقط؛ تُفتح ثانيةً فقط بزر "إعادة تحديد المدة" */
  protected readonly durationEditing = signal(false);

  protected saveDuration(): void {
    const detail = this.stageDetail();
    if (!detail || this.durationSaving()) {
      return;
    }
    this.durationSaving.set(true);
    this.financial.setStageDuration(this.subProjectId, detail.stage, this.durationInput()).subscribe({
      next: () => {
        this.durationSaving.set(false);
        this.durationEditing.set(false);
        this.toast.success('تم حفظ المدة القصوى');
        this.reload();
      },
      error: (err) => {
        this.durationSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر حفظ المدة القصوى');
      },
    });
  }

  protected reopenDurationEdit(): void {
    this.durationEditing.set(true);
  }

  protected cancelDurationEdit(): void {
    this.durationInput.set(this.stageDetail()?.durationDays ?? null);
    this.durationEditing.set(false);
  }

  // ===== تاريخ الإعلان (خاص بمرحلة الإعلان) =====
  protected readonly announcementDateInput = signal('');
  protected readonly announcementDateSaving = signal(false);

  protected saveAnnouncementDate(): void {
    const detail = this.stageDetail();
    if (!detail || this.announcementDateSaving()) {
      return;
    }
    if (!this.announcementDateInput()) {
      this.toast.error('برجاء تحديد تاريخ الإعلان');
      return;
    }
    this.announcementDateSaving.set(true);
    this.financial.setAnnouncementDate(this.subProjectId, this.announcementDateInput()).subscribe({
      next: () => {
        this.announcementDateSaving.set(false);
        this.toast.success('تم حفظ تاريخ الإعلان');
        this.reload();
      },
      error: (err) => {
        this.announcementDateSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر حفظ تاريخ الإعلان');
      },
    });
  }

  // ===== "هذه المرحلة غير لازمة للطرح" =====
  protected readonly showSkipModal = signal(false);
  protected readonly skipReasonInput = signal('');
  protected readonly skipSaving = signal(false);

  protected openSkipModal(): void {
    this.skipReasonInput.set('');
    this.showSkipModal.set(true);
  }

  protected confirmSkip(): void {
    const detail = this.stageDetail();
    if (!detail || this.skipSaving()) {
      return;
    }
    if (!this.skipReasonInput().trim()) {
      this.toast.error('سبب التخطي مطلوب');
      return;
    }
    this.skipSaving.set(true);
    this.financial.skipStage(this.subProjectId, detail.stage, this.skipReasonInput().trim()).subscribe({
      next: () => {
        this.skipSaving.set(false);
        this.showSkipModal.set(false);
        this.toast.success('تم تخطي المرحلة');
        this.reload();
      },
      error: (err) => {
        this.skipSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر تخطي المرحلة');
      },
    });
  }

  // ===== فشل المرحلة → مذكرة عرض جديدة =====
  protected readonly showFailModal = signal(false);
  protected readonly failReasonInput = signal('');
  protected readonly failSaving = signal(false);

  protected openFailModal(): void {
    this.failReasonInput.set('');
    this.showFailModal.set(true);
  }

  protected confirmFail(): void {
    const detail = this.stageDetail();
    if (!detail || this.failSaving()) {
      return;
    }
    if (!this.failReasonInput().trim()) {
      this.toast.error('سبب الفشل مطلوب');
      return;
    }
    this.failSaving.set(true);
    this.financial.failStage(this.subProjectId, detail.stage, this.failReasonInput().trim()).subscribe({
      next: () => {
        this.failSaving.set(false);
        this.showFailModal.set(false);
        this.toast.success('سُجِّل الفشل — أنشئ مذكرة عرض جديدة لإعادة الطرح');
        // إعادة الطرح تبدأ بمذكرة عرض جديدة — ننتقل مباشرة لصفحتها مع تجهيز المشروع الفرعي مُختارًا
        this.router.navigate(['/app/financial/memos'], {
          queryParams: { subProjectId: this.subProjectId, openCreate: 1 },
        });
      },
      error: (err) => {
        this.failSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر تسجيل الفشل');
      },
    });
  }

  /** نص مختصر يوضّح الموعد النهائي — قبله "متبقي" وبعده تحذير أحمر ضمنيًا عبر canFail */
  protected deadlineText(stage: ProcurementStage): string {
    if (!stage.deadline) {
      return '';
    }
    const days = Math.ceil((new Date(stage.deadline).getTime() - Date.now()) / 86_400_000);
    if (days > 0) {
      return `متبقي ${days} ${days === 1 ? 'يوم' : 'أيام'} — حتى ${this.dateStr(stage.deadline)}`;
    }
    if (days === 0) {
      return `آخر يوم — ${this.dateStr(stage.deadline)}`;
    }
    return `تجاوز الموعد النهائي (${this.dateStr(stage.deadline)}) بـ ${-days} ${-days === 1 ? 'يوم' : 'أيام'}`;
  }

  protected reload(): void {
    this.financial.getOverview(this.subProjectId).subscribe({
      next: (overview) => {
        this.overview.set(overview);
        this.loading.set(false);
        const open = this.openStage();
        if (open) {
          this.loadStage(open);
        }
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'تعذر تحميل مراحل الطرح');
        this.loading.set(false);
      },
    });
  }

  protected toggleStage(stage: ProcurementStage): void {
    if (stage.isLocked) {
      return;
    }
    if (this.openStage() === stage.stage) {
      this.openStage.set(null);
      this.stageDetail.set(null);
      this.closeUpload();
      return;
    }
    this.openStage.set(stage.stage);
    this.closeUpload();
    this.loadStage(stage.stage);
  }

  private loadStage(stage: string): void {
    this.stageLoading.set(true);
    if (stage === 'contract-award') {
      this.loadAwardLookups();
    }
    this.financial.getStage(this.subProjectId, stage).subscribe({
      next: (detail) => {
        this.stageDetail.set(detail);
        this.syncAwardForm(detail.contractAward);
        this.durationInput.set(detail.durationDays);
        this.durationEditing.set(false);
        this.announcementDateInput.set(detail.announcementDate?.slice(0, 10) ?? '');
        this.stageLoading.set(false);
      },
      error: () => this.stageLoading.set(false),
    });
  }

  protected statusLabel(stage: ProcurementStage): string {
    if (stage.isCompleted) {
      return 'مكتملة';
    }
    return stage.currentVersionNumber > 0 ? 'جارية' : 'لم تبدأ';
  }

  protected statusClass(stage: ProcurementStage): string {
    if (stage.isCompleted) {
      return 'done';
    }
    return stage.currentVersionNumber > 0 ? 'active' : '';
  }

  // ===== رفع إصدار =====
  protected openUploadForm(): void {
    this.uploadNotes.set('');
    this.uploadFiles.set({});
    this.uploadError.set(null);
    this.showUpload.set(true);
    // النموذج بيتفتح تحت أقسام تانية طويلة (بيانات الترسية، تسليم الأرضية…) فمش بيبان للمستخدم
    // غير لو دوّر لتحت — نسكرول له فور ما يترسم
    setTimeout(() => {
      document.querySelector('.upload-form')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  protected closeUpload(): void {
    this.showUpload.set(false);
    this.uploadError.set(null);
  }

  protected onFilePicked(key: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.setFile(key, file);
    }
    input.value = '';
  }

  protected setFile(key: string, file: File): void {
    this.uploadFiles.set({ ...this.uploadFiles(), [key]: file });
  }

  protected clearFile(key: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    const current = { ...this.uploadFiles() };
    delete current[key];
    this.uploadFiles.set(current);
  }

  protected pickedFile(key: string): File | undefined {
    return this.uploadFiles()[key];
  }

  protected onDragOver(key: string, event: DragEvent): void {
    event.preventDefault();
    this.dragOverKey.set(key);
  }

  protected onDragLeave(key: string): void {
    if (this.dragOverKey() === key) {
      this.dragOverKey.set(null);
    }
  }

  protected onDrop(key: string, event: DragEvent): void {
    event.preventDefault();
    this.dragOverKey.set(null);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.setFile(key, file);
    }
  }

  protected submitUpload(): void {
    const detail = this.stageDetail();
    if (!detail) {
      return;
    }

    const files = this.uploadFiles();
    if (Object.keys(files).length === 0) {
      this.uploadError.set('اختر ملفًا واحدًا على الأقل');
      return;
    }
    for (const slot of detail.fileSlots.filter((s) => s.required)) {
      if (!files[slot.key]) {
        this.uploadError.set(`ملف "${slot.label}" مطلوب`);
        return;
      }
    }

    this.uploading.set(true);
    this.uploadError.set(null);
    this.financial
      .uploadStageVersion(this.subProjectId, detail.stage, files, this.uploadNotes())
      .subscribe({
        next: () => {
          this.uploading.set(false);
          this.showUpload.set(false);
          this.reload();
        },
        error: (err) => {
          this.uploading.set(false);
          this.uploadError.set(err?.error?.message ?? 'تعذر رفع الإصدار');
        },
      });
  }

  // ===== تحميل ملف =====
  protected download(version: ProcurementVersion, fileKey: string, fileName: string): void {
    const detail = this.stageDetail();
    if (!detail) {
      return;
    }
    this.financial
      .downloadStageFile(this.subProjectId, detail.stage, version.versionNumber, fileKey)
      .subscribe((blob) => this.financial.saveBlob(blob, fileName));
  }

  // ===== إكمال / إعادة فتح =====
  protected complete(): void {
    const detail = this.stageDetail();
    if (!detail || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.financial.completeStage(this.subProjectId, detail.stage).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success('تم إكمال المرحلة');
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر إكمال المرحلة');
      },
    });
  }

  protected reopen(): void {
    const detail = this.stageDetail();
    if (!detail || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.financial.reopenStage(this.subProjectId, detail.stage).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success('تم إعادة فتح المرحلة');
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        this.toast.error(err?.error?.message ?? 'تعذر إعادة فتح المرحلة');
      },
    });
  }

  protected fileSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} بايت`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} ك.ب`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} م.ب`;
  }

  protected dateStr(value: string | null): string {
    return value ? new Date(value).toLocaleDateString('ar-EG', { year: 'numeric', month: 'long', day: 'numeric' }) : '—';
  }
}
