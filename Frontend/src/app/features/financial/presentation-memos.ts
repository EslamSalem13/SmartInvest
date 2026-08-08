import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { FinancialService } from '../../core/services/financial.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { Roles } from '../../core/models/auth.models';
import { FinancialYear } from '../../core/models/project.models';
import {
  PresentationMemo,
  PresentationMemoDetail,
  ProcurementSubProjectListItem,
  ProcurementVersion,
} from '../../core/models/financial.models';

@Component({
  selector: 'app-presentation-memos',
  imports: [RouterLink, FormsModule],
  templateUrl: './presentation-memos.html',
  styleUrl: './financial.css',
})
export class PresentationMemos implements OnInit {
  private readonly financial = inject(FinancialService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly memos = signal<PresentationMemo[]>([]);
  protected readonly subProjects = signal<ProcurementSubProjectListItem[]>([]);

  /** السنة المالية — المذكرة تُبنى من مشروعات السنة المفتوحة حاليًا فقط */
  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly selectedYearId = signal<number | null>(null);
  protected readonly sortedYears = computed(() =>
    [...this.financialYears()].sort((a, b) => b.startDate.localeCompare(a.startDate)),
  );

  /** فلاتر القائمة */
  protected readonly statusFilter = signal<'all' | 'not-started' | 'active' | 'completed'>('all');
  protected readonly searchTerm = signal('');

  protected readonly filteredMemos = computed(() => {
    const status = this.statusFilter();
    const term = this.searchTerm().trim();

    return this.memos().filter((memo) => {
      const memoStatus = this.memoStatus(memo);
      if (status !== 'all' && memoStatus !== status) {
        return false;
      }
      if (!term) {
        return true;
      }
      return (
        memo.title.includes(term) ||
        memo.subProjects.some(
          (s) => s.subProjectName.includes(term) || (s.subProjectCode ?? '').includes(term),
        )
      );
    });
  });

  protected readonly countAll = computed(() => this.memos().length);
  protected readonly countNotStarted = computed(
    () => this.memos().filter((m) => this.memoStatus(m) === 'not-started').length,
  );
  protected readonly countActive = computed(
    () => this.memos().filter((m) => this.memoStatus(m) === 'active').length,
  );
  protected readonly countCompleted = computed(
    () => this.memos().filter((m) => this.memoStatus(m) === 'completed').length,
  );

  /** المذكرة المفتوحة (تفاصيل + إصدارات) */
  protected readonly openMemoId = signal<number | null>(null);
  protected readonly memoDetail = signal<PresentationMemoDetail | null>(null);
  protected readonly detailLoading = signal(false);

  /** نموذج إنشاء/تعديل */
  protected readonly showForm = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly fTitle = signal('');
  protected readonly fSubProjectIds = signal<number[]>([]);
  protected readonly formError = signal<string | null>(null);
  protected readonly saving = signal(false);

  /** بحث داخل قائمة المشروعات في النموذج */
  protected readonly pickerSearch = signal('');

  /**
   * المشروعات المعروضة في النموذج. المشروع المُختار يظل ظاهرًا دائمًا حتى لو
   * لم يطابق البحث — وإلا اختفى اختيار المستخدم من أمامه قبل الحفظ.
   */
  protected readonly filteredSubProjects = computed(() => {
    const term = this.pickerSearch().trim();
    if (!term) {
      return this.subProjects();
    }
    return this.subProjects().filter(
      (sub) =>
        this.isChecked(sub.subProjectId) ||
        sub.subProjectName.includes(term) ||
        (sub.subProjectCode ?? '').includes(term),
    );
  });

  /** نموذج رفع إصدار */
  protected readonly showUpload = signal(false);
  protected readonly uploadFile = signal<File | null>(null);
  protected readonly uploadNotes = signal('');
  protected readonly uploadError = signal<string | null>(null);
  protected readonly uploading = signal(false);
  protected readonly dragOverMemoFile = signal(false);

  protected readonly busy = signal(false);

  protected readonly isStaff = computed(() => {
    const role = this.auth.role();
    return role === Roles.PlanningManager || role === Roles.PlanningEmployee;
  });
  protected readonly isManager = this.auth.isManager;

  ngOnInit(): void {
    this.reload();
    this.financialYearsService.getAll().subscribe({
      next: (years) => {
        this.financialYears.set(years);
        const sorted = [...years].sort((a, b) => b.startDate.localeCompare(a.startDate));
        if (sorted.length > 0) {
          this.selectedYearId.set(sorted[0].id);
        }
        this.loadSubProjects();
      },
      error: () => this.loadSubProjects(),
    });
  }

  protected onYearChange(id: number | null): void {
    this.selectedYearId.set(id);
    this.fSubProjectIds.set([]);
    this.pickerSearch.set('');
    this.loadSubProjects();
  }

  private loadSubProjects(): void {
    this.financial.getSubProjects(this.selectedYearId()).subscribe({
      next: (items) => this.subProjects.set(items),
    });
  }

  protected reload(): void {
    this.financial.getMemos().subscribe({
      next: (memos) => {
        this.memos.set(memos);
        this.loading.set(false);
        const open = this.openMemoId();
        if (open != null) {
          this.loadDetail(open);
        }
      },
      error: () => {
        this.error.set('تعذر تحميل مذكرات العرض');
        this.loading.set(false);
      },
    });
  }

  protected toggleMemo(memo: PresentationMemo): void {
    if (this.openMemoId() === memo.id) {
      this.openMemoId.set(null);
      this.memoDetail.set(null);
      this.closeUpload();
      return;
    }
    this.openMemoId.set(memo.id);
    this.closeUpload();
    this.loadDetail(memo.id);
  }

  private loadDetail(id: number): void {
    this.detailLoading.set(true);
    this.financial.getMemo(id).subscribe({
      next: (detail) => {
        this.memoDetail.set(detail);
        this.detailLoading.set(false);
      },
      error: () => this.detailLoading.set(false),
    });
  }

  // ===== إنشاء / تعديل =====
  protected openCreate(): void {
    this.editingId.set(null);
    this.fTitle.set('');
    this.fSubProjectIds.set([]);
    this.formError.set(null);
    this.pickerSearch.set('');
    this.showForm.set(true);
  }

  protected openEdit(memo: PresentationMemo): void {
    this.editingId.set(memo.id);
    this.fTitle.set(memo.title);
    this.fSubProjectIds.set(memo.subProjects.map((x) => x.subProjectId));
    this.formError.set(null);
    this.pickerSearch.set('');
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected isChecked(subProjectId: number): boolean {
    return this.fSubProjectIds().includes(subProjectId);
  }

  protected toggleSubProject(subProjectId: number): void {
    const current = this.fSubProjectIds();
    this.fSubProjectIds.set(
      current.includes(subProjectId)
        ? current.filter((x) => x !== subProjectId)
        : [...current, subProjectId],
    );
  }

  protected saveForm(): void {
    if (!this.fTitle().trim()) {
      this.formError.set('عنوان المذكرة مطلوب');
      return;
    }
    if (this.fSubProjectIds().length === 0) {
      this.formError.set('اختر مشروعًا فرعيًا واحدًا على الأقل');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);
    const dto = { title: this.fTitle().trim(), subProjectIds: this.fSubProjectIds() };
    const id = this.editingId();
    const request = id == null ? this.financial.createMemo(dto) : this.financial.updateMemo(id, dto);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.reload();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذر الحفظ');
      },
    });
  }

  protected deleteMemo(memo: PresentationMemo): void {
    if (!confirm(`حذف مذكرة العرض "${memo.title}"؟`)) {
      return;
    }
    this.financial.deleteMemo(memo.id).subscribe({
      next: () => {
        if (this.openMemoId() === memo.id) {
          this.openMemoId.set(null);
          this.memoDetail.set(null);
        }
        this.reload();
      },
      error: (err) => alert(err?.error?.message ?? 'تعذر الحذف'),
    });
  }

  // ===== الإصدارات =====
  protected openUploadForm(): void {
    this.uploadFile.set(null);
    this.uploadNotes.set('');
    this.uploadError.set(null);
    this.showUpload.set(true);
  }

  protected closeUpload(): void {
    this.showUpload.set(false);
    this.uploadError.set(null);
  }

  protected onFilePicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.uploadFile.set(input.files?.[0] ?? null);
    input.value = '';
  }

  protected clearMemoFile(event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.uploadFile.set(null);
  }

  protected onMemoDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragOverMemoFile.set(true);
  }

  protected onMemoDragLeave(): void {
    this.dragOverMemoFile.set(false);
  }

  protected onMemoDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOverMemoFile.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.uploadFile.set(file);
    }
  }

  protected submitUpload(): void {
    const id = this.openMemoId();
    const file = this.uploadFile();
    if (id == null) {
      return;
    }
    if (!file) {
      this.uploadError.set('اختر ملف المذكرة');
      return;
    }

    this.uploading.set(true);
    this.uploadError.set(null);
    this.financial.uploadMemoVersion(id, file, this.uploadNotes()).subscribe({
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

  protected download(version: ProcurementVersion): void {
    const id = this.openMemoId();
    if (id == null) {
      return;
    }
    const fileName = version.files[0]?.fileName ?? 'memo';
    this.financial
      .downloadMemoFile(id, version.versionNumber)
      .subscribe((blob) => this.financial.saveBlob(blob, fileName));
  }

  protected complete(): void {
    const id = this.openMemoId();
    if (id == null || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.financial.completeMemo(id).subscribe({
      next: () => {
        this.busy.set(false);
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        alert(err?.error?.message ?? 'تعذر إكمال المذكرة');
      },
    });
  }

  protected reopen(): void {
    const id = this.openMemoId();
    if (id == null || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.financial.reopenMemo(id).subscribe({
      next: () => {
        this.busy.set(false);
        this.reload();
      },
      error: (err) => {
        this.busy.set(false);
        alert(err?.error?.message ?? 'تعذر إعادة الفتح');
      },
    });
  }

  protected memoStatus(memo: PresentationMemo): 'not-started' | 'active' | 'completed' {
    if (memo.isCompleted) {
      return 'completed';
    }
    return memo.currentVersionNumber > 0 ? 'active' : 'not-started';
  }

  protected memoStatusLabel(memo: PresentationMemo): string {
    switch (this.memoStatus(memo)) {
      case 'completed':
        return 'مكتملة';
      case 'active':
        return 'جارية';
      default:
        return 'لم تبدأ';
    }
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
    return value
      ? new Date(value).toLocaleDateString('ar-EG', { year: 'numeric', month: 'long', day: 'numeric' })
      : '—';
  }
}
