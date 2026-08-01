import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ImportService } from '../../core/services/import.service';
import {
  ImportCommit,
  ImportCommitResult,
  ImportPreviewResult,
  ImportResolution,
  ImportRowResolution,
  MainProjectCodeResolution,
} from '../../core/models/project.models';

type Step = 'upload' | 'reconcile' | 'confirm' | 'result';

@Component({
  selector: 'app-excel-import-wizard',
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="si-overlay" (click)="close.emit()">
        <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(720px,100%)">
          <div class="si-modal-head">
            <div class="grow"><h3>استيراد مشروعات من Excel</h3></div>
            <button class="si-x" (click)="close.emit()" aria-label="إغلاق">×</button>
          </div>
          <div class="si-modal-body">
            @if (error()) { <div class="si-err">{{ error() }}</div> }

            @if (step() === 'upload') {
              <div class="si-fld full">
                <label>ملف الخطة (Excel) <span class="req">*</span></label>
                <input type="file" accept=".xlsx" (change)="onFileSelected($event)" />
                <p class="hint">ارفع ملف بصيغة .xlsx يحتوي على ورقة بيانات واحدة فقط للمشروعات (بدون أوراق إضافية مكررة أو غير مرتبطة).</p>
              </div>
            }

            @if (step() === 'reconcile' || step() === 'confirm') {
              <div class="mode-banner">
                @if (preview()?.mode === 'Suggested') { تم اكتشاف: خطة مقترحة (لا يوجد أكواد مشروعات) }
                @else { تم اكتشاف: خطة معتمدة (كل الصفوف تحتوي على كود مشروع) }
              </div>
            }

            @if (step() === 'reconcile' && preview()?.mode === 'Suggested' && preview()?.suggested; as s) {
              @for (group of suggestedCategories(s); track group.key) {
                @if (group.items.length > 0) {
                  <div class="si-fld full">
                    <label>{{ group.label }}</label>
                    @for (item of group.items; track item.name) {
                      <div class="recon-row">
                        <span class="recon-name">{{ item.name }}</span>
                        <span class="recon-count">({{ item.rowCount }} صف)</span>
                        <label class="recon-choice"><input type="radio" [name]="group.key + '-' + item.name" [checked]="isNew(group.key, item.name)" (change)="setNew(group.key, item.name)" /> جديد</label>
                        <label class="recon-choice"><input type="radio" [name]="group.key + '-' + item.name" [checked]="!isNew(group.key, item.name)" (change)="setNew(group.key, item.name, false)" /> نفس القيمة</label>
                      </div>
                    }
                  </div>
                }
              }
              @if (s.mainProjectCodeConflicts.length > 0) {
                <div class="si-fld full">
                  <label>تعارض أكواد مشروعات رئيسية</label>
                  @for (conflict of s.mainProjectCodeConflicts; track conflict.code) {
                    <div class="recon-row">
                      <span class="recon-name">كود {{ conflict.code }}</span>
                      @for (opt of conflict.options; track opt.mainProjectName) {
                        <label class="recon-choice">
                          <input type="radio" [name]="'code-' + conflict.code" (change)="chooseCodeOption(conflict.code, opt)" />
                          {{ opt.mainProjectName }} ({{ opt.mainProgramName }})
                        </label>
                      }
                      <label class="recon-choice"><input type="radio" [name]="'code-' + conflict.code" [checked]="true" (change)="clearCodeChoice(conflict.code)" /> إبقاؤهما منفصلين</label>
                    </div>
                  }
                </div>
              }
            }

            @if (step() === 'reconcile' && preview()?.mode === 'Approved' && preview()?.approved; as a) {
              @if (a.unresolvedRows.length === 0) {
                <p>تم مطابقة جميع الصفوف بنجاح.</p>
              } @else {
                @for (row of a.unresolvedRows; track row.rowIndex) {
                  <div class="recon-row">
                    <span class="recon-name">{{ row.mainProjectName }} / {{ row.subProjectName }} (كود {{ row.code }})</span>
                    <label class="recon-choice"><input type="radio" [name]="'row-' + row.rowIndex" [checked]="true" (change)="setRowCreateNew(row.rowIndex)" /> إنشاء جديد (معتمد)</label>
                    <label class="recon-choice"><input type="radio" [name]="'row-' + row.rowIndex" (change)="setRowExisting(row.rowIndex, existingSubProjectId(row.rowIndex))" /> ربط بمشروع موجود، رقم:</label>
                    <input type="number" [ngModel]="existingSubProjectId(row.rowIndex)" (ngModelChange)="setRowExisting(row.rowIndex, $event)" style="width:90px" />
                  </div>
                }
              }
            }

            @if (step() === 'confirm') {
              <p>
                @if (preview()?.mode === 'Suggested') {
                  سيتم إنشاء {{ preview()?.suggested?.mainProjectCount }} مشروع رئيسي و{{ preview()?.suggested?.subProjectCount }} مشروع فرعي ضمن خطة مقترحة للسنة المالية المحددة.
                } @else {
                  سيتم اعتماد {{ preview()?.approved?.matchedCount }} مشروع مطابق.
                }
              </p>
              @if (preview()?.mode === 'Approved') {
                <div class="si-fld"><label>تاريخ الاعتماد <span class="req">*</span></label><input type="date" [ngModel]="approvalDate()" (ngModelChange)="approvalDate.set($event)" /></div>
              }
            }

            @if (step() === 'result' && result(); as r) {
              <p>تم الاستيراد بنجاح — الخطة: {{ r.planName }} ({{ r.planStatus }})</p>
              @if (r.mode === 'Suggested') {
                <p>مشروعات رئيسية: {{ r.mainProjectsCreated }} — مشروعات فرعية: {{ r.subProjectsCreated }}</p>
              } @else {
                <p>معتمدة: {{ r.subProjectsApproved }} — جديدة ومعتمدة: {{ r.subProjectsCreatedAndApproved }}</p>
              }
              @if (r.failed.length > 0) {
                <div class="si-err">
                  فشل استيراد {{ r.failed.length }} صف:
                  @for (f of r.failed; track f.name) { <div>{{ f.name }}: {{ f.reason }}</div> }
                </div>
              }
            }
          </div>
          <div class="si-modal-foot">
            @if (step() === 'upload') {
              <button class="si-btn primary" [disabled]="!selectedFile() || uploading()" (click)="submitUpload()">
                @if (uploading()) { جاري الرفع… } @else { رفع ومتابعة }
              </button>
            }
            @if (step() === 'reconcile') {
              <button class="si-btn primary" (click)="step.set('confirm')">التالي</button>
            }
            @if (step() === 'confirm') {
              <button class="si-btn primary" [disabled]="committing()" (click)="submitCommit()">
                @if (committing()) { جاري الحفظ… } @else { تأكيد الاستيراد }
              </button>
            }
            @if (step() === 'result') {
              <button class="si-btn primary" (click)="finish()">تم</button>
            }
            <button class="si-btn" (click)="close.emit()">إلغاء</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .hint { font-size: 12px; color: var(--muted); margin: 6px 0 0; }
    .mode-banner { background: var(--surface-2); border-radius: 9px; padding: 10px 12px; font-weight: 700; font-size: 13px; margin-bottom: 14px; }
    .recon-row { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding: 8px 0; border-bottom: 1px solid var(--line); font-size: 13px; }
    .recon-name { font-weight: 700; }
    .recon-count { color: var(--muted); font-size: 12px; }
    .recon-choice { display: flex; align-items: center; gap: 5px; font-size: 12.5px; }
  `],
})
export class ExcelImportWizard {
  private readonly importService = inject(ImportService);

  readonly open = input(false);
  readonly financialYearId = input.required<number | null>();
  readonly close = output<void>();
  readonly saved = output<void>();

  protected readonly step = signal<Step>('upload');
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly committing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly preview = signal<ImportPreviewResult | null>(null);
  protected readonly result = signal<ImportCommitResult | null>(null);
  protected readonly approvalDate = signal<string>(new Date().toISOString().slice(0, 10));

  private readonly resolutions: Record<string, Map<string, ImportResolution>> = {
    markaz: new Map(), mainProgram: new Map(), subProgram: new Map(), agency: new Map(),
    projectLevel: new Map(), componentType: new Map(), accountingUnit: new Map(),
  };
  private readonly codeResolutions = new Map<string, MainProjectCodeResolution>();
  private readonly rowResolutions = new Map<number, ImportRowResolution>();

  protected suggestedCategories(s: NonNullable<ImportPreviewResult['suggested']>) {
    return [
      { key: 'markaz', label: 'مراكز غير معروفة', items: s.unresolvedMarkaz },
      { key: 'mainProgram', label: 'برامج رئيسية غير معروفة', items: s.unresolvedMainPrograms },
      { key: 'subProgram', label: 'برامج فرعية غير معروفة', items: s.unresolvedSubPrograms },
      { key: 'agency', label: 'جهات منفذة غير معروفة', items: s.unresolvedAgencies },
      { key: 'projectLevel', label: 'مستويات مشروع غير معروفة', items: s.unresolvedProjectLevels },
      { key: 'componentType', label: 'مكوّنات عينية غير معروفة', items: s.unresolvedComponentTypes },
      { key: 'accountingUnit', label: 'وحدات حسابية غير معروفة', items: s.unresolvedAccountingUnits },
    ];
  }

  protected isNew(category: string, name: string): boolean {
    return this.resolutions[category].get(name)?.createNew ?? true;
  }

  protected setNew(category: string, name: string, createNew = true): void {
    this.resolutions[category].set(name, { name, createNew, existingId: null });
  }

  protected chooseCodeOption(code: string, opt: { mainProjectName: string; mainProgramName: string }): void {
    this.codeResolutions.set(code, { code, chosenMainProjectName: opt.mainProjectName, chosenMainProgramName: opt.mainProgramName });
  }

  protected clearCodeChoice(code: string): void {
    this.codeResolutions.delete(code);
  }

  protected existingSubProjectId(rowIndex: number): number | null {
    return this.rowResolutions.get(rowIndex)?.existingSubProjectId ?? null;
  }

  protected setRowCreateNew(rowIndex: number): void {
    this.rowResolutions.set(rowIndex, { rowIndex, createNew: true, existingSubProjectId: null });
  }

  protected setRowExisting(rowIndex: number, subProjectId: number | null): void {
    this.rowResolutions.set(rowIndex, { rowIndex, createNew: false, existingSubProjectId: subProjectId });
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  protected submitUpload(): void {
    const file = this.selectedFile();
    if (!file || this.uploading()) return;

    this.uploading.set(true);
    this.error.set(null);
    this.importService.preview(file).subscribe({
      next: (result) => {
        this.uploading.set(false);
        this.preview.set(result);
        this.step.set('reconcile');
      },
      error: (err) => {
        this.uploading.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر معالجة الملف');
      },
    });
  }

  protected submitCommit(): void {
    const preview = this.preview();
    const yearId = this.financialYearId();
    if (!preview || yearId == null || this.committing()) return;

    if (preview.mode === 'Approved' && !this.approvalDate()) {
      this.error.set('برجاء إدخال تاريخ الاعتماد');
      return;
    }

    this.committing.set(true);
    this.error.set(null);

    const dto: ImportCommit = {
      importId: preview.importId,
      financialYearId: yearId,
      approvalDate: preview.mode === 'Approved' ? this.approvalDate() : null,
      markazResolutions: [...this.resolutions['markaz'].values()],
      mainProgramResolutions: [...this.resolutions['mainProgram'].values()],
      subProgramResolutions: [...this.resolutions['subProgram'].values()],
      agencyResolutions: [...this.resolutions['agency'].values()],
      projectLevelResolutions: [...this.resolutions['projectLevel'].values()],
      componentTypeResolutions: [...this.resolutions['componentType'].values()],
      accountingUnitResolutions: [...this.resolutions['accountingUnit'].values()],
      mainProjectCodeResolutions: [...this.codeResolutions.values()],
      rowResolutions: [...this.rowResolutions.values()],
    };

    this.importService.commit(dto).subscribe({
      next: (result) => {
        this.committing.set(false);
        this.result.set(result);
        this.step.set('result');
      },
      error: (err) => {
        this.committing.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر إتمام الاستيراد');
      },
    });
  }

  protected finish(): void {
    this.reset();
    this.saved.emit();
  }

  private reset(): void {
    this.step.set('upload');
    this.selectedFile.set(null);
    this.preview.set(null);
    this.result.set(null);
    this.error.set(null);
    for (const map of Object.values(this.resolutions)) map.clear();
    this.codeResolutions.clear();
    this.rowResolutions.clear();
  }
}
