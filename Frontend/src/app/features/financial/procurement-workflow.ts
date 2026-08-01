import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { FinancialService } from '../../core/services/financial.service';
import { Roles } from '../../core/models/auth.models';
import {
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
  private readonly auth = inject(AuthService);

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

  ngOnInit(): void {
    this.reload();
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
    this.financial.getStage(this.subProjectId, stage).subscribe({
      next: (detail) => {
        this.stageDetail.set(detail);
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
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        alert(err?.error?.message ?? 'تعذر إكمال المرحلة');
      },
    });
  }

  // ===== الدفعة المقدمة 25% (مرحلة العقد والترسية فقط) =====
  protected toggleAdvancePayment(done: boolean): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.financial.setAdvancePaymentDone(this.subProjectId, done).subscribe({
      next: () => {
        this.busy.set(false);
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        alert(err?.error?.message ?? 'تعذر تحديث حالة الدفعة المقدمة');
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
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        alert(err?.error?.message ?? 'تعذر إعادة فتح المرحلة');
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
