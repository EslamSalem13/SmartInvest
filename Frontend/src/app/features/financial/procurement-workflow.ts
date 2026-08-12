import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { FinancialService } from '../../core/services/financial.service';
import { ToastService } from '../../core/services/toast.service';
import { ContractorsService } from '../../core/services/contractors.service';
import { ContractTypesService } from '../../core/services/contract-types.service';
import { Roles } from '../../core/models/auth.models';
import { Contractor, Lookup } from '../../core/models/project.models';
import { formatEgpAsThousands } from '../../core/utils/budget.util';
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
  private readonly financial = inject(FinancialService);
  private readonly contractorsService = inject(ContractorsService);
  private readonly contractTypesService = inject(ContractTypesService);
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

  protected readonly isStaff = computed(() => {
    const role = this.auth.role();
    return role === Roles.PlanningManager || role === Roles.PlanningEmployee;
  });
  protected readonly isManager = this.auth.isManager;

  protected readonly completedCount = computed(
    () => this.overview()?.stages.filter((s) => s.isCompleted).length ?? 0,
  );

  // ===== مرحلة الترسية =====
  protected readonly contractors = signal<Contractor[]>([]);
  protected readonly contractTypes = signal<Lookup[]>([]);

  protected readonly award = computed(() => this.stageDetail()?.contractAward ?? null);

  /** نموذج الترسية — إشارة لكل حقل، على نمط باقي النماذج في المشروع */
  protected readonly aContractorId = signal<number | null>(null);
  protected readonly aContractTypeId = signal<number | null>(null);
  protected readonly aContractNumber = signal('');
  protected readonly aContractValue = signal<number | null>(null);
  protected readonly aAdvanceDone = signal(false);
  protected readonly aAdvancePercentage = signal<number | null>(null);
  protected readonly aAdvanceSelf = signal<number | null>(null);
  protected readonly aAdvanceBank = signal<number | null>(null);
  protected readonly aDurationMonths = signal<number | null>(null);
  protected readonly aDurationDays = signal<number | null>(null);
  protected readonly aHandoverMode = signal<number | null>(null);
  protected readonly aPenaltyAmount = signal<number | null>(null);
  protected readonly awardSaving = signal(false);
  protected readonly awardError = signal<string | null>(null);

  protected readonly aHandoverDate = signal<string>('');
  protected readonly aHandoverSaving = signal(false);
  protected aHandoverFile: File | null = null;

  /** قيمة الدفعة المقدمة بالجنيه — تظهر تلقائيًا بمجرد كتابة النسبة */
  protected readonly advanceAmount = computed(() => {
    const pct = this.aAdvancePercentage();
    const total = this.award()?.totalCost ?? 0;
    if (pct == null || pct <= 0) {
      return 0;
    }
    return Math.round(total * pct) / 100;
  });

  /** ما تبقّى ليتوازن التقسيم بين الذاتي والبنكي */
  protected readonly advanceRemaining = computed(
    () => Math.round((this.advanceAmount() - (this.aAdvanceSelf() ?? 0) - (this.aAdvanceBank() ?? 0)) * 100) / 100,
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
    this.contractTypesService.getAll().subscribe({
      next: (items) => this.contractTypes.set(items),
    });
  }

  /** يملأ النموذج من الخادم — يُستدعى كلما أُعيد تحميل تفاصيل المرحلة */
  private syncAwardForm(details: ContractAwardDetails | null | undefined): void {
    this.awardError.set(null);
    if (!details) {
      return;
    }
    this.aContractorId.set(details.contractorId);
    this.aContractTypeId.set(details.contractTypeId);
    this.aContractNumber.set(details.contractNumber ?? '');
    this.aContractValue.set(details.contractValue);
    this.aAdvanceDone.set(details.advancePaymentDone);
    this.aAdvancePercentage.set(details.advancePaymentPercentage);
    this.aAdvanceSelf.set(details.advancePaymentSelfAmount);
    this.aAdvanceBank.set(details.advancePaymentBankAmount);
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
        advancePaymentSelfAmount: this.aAdvanceSelf(),
        advancePaymentBankAmount: this.aAdvanceBank(),
        executionDurationMonths: this.aDurationMonths(),
        executionDurationDays: this.aDurationDays(),
        siteHandoverMode: this.aHandoverMode(),
        penaltyAmount: this.aPenaltyAmount(),
        contractorId: this.aContractorId(),
        contractTypeId: this.aContractTypeId(),
        contractNumber: this.aContractNumber().trim() || null,
        contractValue: this.aContractValue(),
      })
      .subscribe({
        next: () => {
          this.awardSaving.set(false);
          this.toast.success('تم حفظ بيانات الترسية');
          this.reload();
        },
        error: (err) => {
          this.awardSaving.set(false);
          this.awardError.set(err?.error?.message ?? 'تعذر حفظ بيانات الترسية');
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
