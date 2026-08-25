import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ImportService } from '../../core/services/import.service';
import { LookupsService } from '../../core/services/lookups.service';
import { AgenciesService } from '../../core/services/agencies.service';
import { ProjectsService } from '../../core/services/projects.service';
import { MeasurementsService } from '../../core/services/measurements.service';
import {
  ExtractedMeasurement,
  ImportCommit,
  ImportCommitResult,
  ImportPreviewResult,
  ImportResolution,
  ImportRowResolution,
  Lookup,
  MainProjectCodeResolution,
  RowMeasurementPreview,
} from '../../core/models/project.models';

type Step = 'upload' | 'reconcile' | 'confirm' | 'result';

@Component({
  selector: 'app-excel-import-wizard',
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="si-overlay" (click)="dismiss()">
        <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(720px,100%)">
          <div class="si-modal-head">
            <div class="grow"><h3>استيراد مشروعات من Excel</h3></div>
            <button class="si-x" (click)="dismiss()" aria-label="إغلاق">×</button>
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
                        @if (item.suggestedMatch) {
                          <span class="recon-suggest">اقتراح: «{{ item.suggestedMatch }}» — راجع قبل التأكيد</span>
                        }
                        <label class="recon-choice"><input type="radio" [name]="group.key + '-' + item.name" [checked]="isNew(group.key, item.name)" (change)="setNew(group.key, item.name)" /> جديد</label>
                        <label class="recon-choice"><input type="radio" [name]="group.key + '-' + item.name" [checked]="!isNew(group.key, item.name)" (change)="setExisting(group.key, item.name, existingIdFor(group.key, item.name))" /> ربط بموجود:</label>
                        <select [ngModel]="existingIdFor(group.key, item.name)" (ngModelChange)="setExisting(group.key, item.name, $event)" [disabled]="isNew(group.key, item.name) || optionsLoading()" style="max-width:160px">
                          @if (optionsLoading()) {
                            <option [ngValue]="null">جارٍ التحميل…</option>
                          } @else {
                            <option [ngValue]="null">— اختر —</option>
                            @for (opt of existingOptionsFor(group.key); track opt.id) {
                              <option [ngValue]="opt.id">{{ opt.name }}</option>
                            }
                          }
                        </select>
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
                      @for (opt of conflict.options; track opt.mainProjectName + '|' + opt.mainProgramName) {
                        <label class="recon-choice">
                          <input type="radio" [name]="'code-' + conflict.code" [checked]="isCodeOption(conflict.code, opt)" (change)="chooseCodeOption(conflict.code, opt)" />
                          {{ opt.mainProjectName }} ({{ opt.mainProgramName }})
                        </label>
                      }
                      <label class="recon-choice"><input type="radio" [name]="'code-' + conflict.code" [checked]="!hasCodeChoice(conflict.code)" (change)="clearCodeChoice(conflict.code)" /> إبقاؤهما منفصلين</label>
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
                  <div class="recon-row" style="flex-direction:column; align-items:stretch;">
                    <div style="display:flex; align-items:center; gap:10px; flex-wrap:wrap;">
                      <span class="recon-name">{{ row.mainProjectName }} / {{ row.subProjectName }} (كود {{ row.code }})</span>
                      @if (row.suggestedMatchLabel) {
                        <span class="recon-suggest">اقتراح: «{{ row.suggestedMatchLabel }}» — راجع قبل التأكيد</span>
                      }
                    </div>
                    <div style="display:flex; align-items:center; gap:10px; flex-wrap:wrap;">
                      <label class="recon-choice"><input type="radio" [name]="'row-' + row.rowIndex" [checked]="isRowCreateNew(row.rowIndex)" (change)="setRowCreateNew(row.rowIndex)" /> إنشاء جديد (معتمد)</label>
                      <label class="recon-choice"><input type="radio" [name]="'row-' + row.rowIndex" [checked]="!isRowCreateNew(row.rowIndex)" (change)="setRowExisting(row.rowIndex, existingSubProjectId(row.rowIndex))" /> ربط بمشروع موجود:</label>
                      <div class="combo-wrap">
                        <input
                          type="text"
                          autocomplete="off"
                          [ngModel]="subProjectLabelFor(row.rowIndex)"
                          (ngModelChange)="onSubProjectLabelChange(row.rowIndex, $event)"
                          (focus)="openSubProjectDropdownRow.set(row.rowIndex)"
                          (blur)="onSubProjectInputBlur(row.rowIndex)"
                          [disabled]="isRowCreateNew(row.rowIndex) || subProjectOptionsLoading()"
                          [placeholder]="subProjectOptionsLoading() ? 'جارٍ التحميل…' : 'اكتب للبحث عن مشروع فرعي…'"
                          [class.invalid]="!isRowCreateNew(row.rowIndex) && existingSubProjectId(row.rowIndex) == null && subProjectLabelFor(row.rowIndex).length > 0"
                        />
                        @if (openSubProjectDropdownRow() === row.rowIndex && filteredSubProjectOptions(row.rowIndex).length > 0) {
                          <div class="combo-list">
                            @for (opt of filteredSubProjectOptions(row.rowIndex); track opt.id) {
                              <button type="button" class="combo-item" (mousedown)="selectSubProjectOption(row.rowIndex, opt)">{{ opt.name }}</button>
                            }
                          </div>
                        }
                      </div>
                    </div>
                  </div>
                }
              }
            }

            @if (step() === 'confirm') {
              <p>
                @if (preview()?.mode === 'Suggested') {
                  سيتم إنشاء {{ preview()?.suggested?.mainProjectCount }} مشروع رئيسي و{{ preview()?.suggested?.subProjectCount }} مشروع فرعي ضمن خطة مقترحة للسنة المالية المحددة.
                } @else {
                  سيتم اعتماد {{ preview()?.approved?.matchedCount }} مشروع مطابق، وربط {{ pendingLinkExistingCount() }} مشروع بمشروعات موجودة، وإنشاء واعتماد {{ pendingCreateNewCount() }} مشروع جديد.
                }
              </p>
              @if (preview()?.mode === 'Approved') {
                <div class="si-fld"><label>تاريخ الاعتماد <span class="req">*</span></label><input type="date" [ngModel]="approvalDate()" (ngModelChange)="approvalDate.set($event)" /></div>
              }
              @if (preview()?.rowMeasurements && preview()!.rowMeasurements.length > 0) {
                <div class="si-fld full">
                  <label>القياسات المستخرَجة من أسماء المشروعات الفرعية (راجعها قبل التأكيد)</label>
                  @for (rowPreview of preview()!.rowMeasurements; track rowPreview.rowIndex) {
                    <div class="recon-row" style="flex-direction:column; align-items:stretch;">
                      <span class="recon-name">{{ rowPreview.subProjectName }}</span>
                      <div class="measure-rows">
                        @for (m of measurementsForRow(rowPreview.rowIndex); track $index; let i = $index) {
                          <div class="measure-row">
                            <div class="si-fld">
                              <label>اسم القياس</label>
                              <input
                                list="import-measurement-names-list"
                                [ngModel]="m.name"
                                (ngModelChange)="updateMeasurement(rowPreview.rowIndex, i, 'name', $event)"
                                placeholder="مثال: عدد"
                              />
                            </div>
                            <div class="si-fld">
                              <label>القيمة</label>
                              <input
                                type="number"
                                [ngModel]="m.value"
                                (ngModelChange)="updateMeasurement(rowPreview.rowIndex, i, 'value', $event)"
                                placeholder="مثال: 3"
                              />
                            </div>
                            <div class="si-fld">
                              <label>الوحدة</label>
                              <input
                                list="import-measurement-units-list"
                                [ngModel]="m.unit"
                                (ngModelChange)="updateMeasurement(rowPreview.rowIndex, i, 'unit', $event)"
                                placeholder="مثال: سيارة 50 طن"
                              />
                            </div>
                            <button type="button" class="si-x sm" (click)="removeMeasurement(rowPreview.rowIndex, i)" aria-label="حذف القياس">×</button>
                          </div>
                        }
                        <button type="button" class="si-btn" (click)="addMeasurement(rowPreview.rowIndex)">
                          <svg viewBox="0 0 24 24" width="15" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
                          إضافة قياس
                        </button>
                      </div>
                    </div>
                  }
                  <datalist id="import-measurement-names-list">
                    @for (n of allMeasurementNames(); track n) { <option [value]="n"></option> }
                  </datalist>
                  <datalist id="import-measurement-units-list">
                    @for (n of allUnitNames(); track n) { <option [value]="n"></option> }
                  </datalist>
                </div>
              }
            }

            @if (step() === 'result' && result(); as r) {
              <p>تم الاستيراد بنجاح — الخطة: {{ r.planName }} ({{ r.planStatus }})</p>
              @if (r.mode === 'Suggested') {
                <p>مشروعات رئيسية جديدة: {{ r.mainProjectsCreated }} — مشروعات فرعية جديدة: {{ r.subProjectsCreated }}</p>
                @if (r.mainProjectsAlreadyExisted > 0) {
                  <p class="hint">{{ r.mainProjectsAlreadyExisted }} مشروع رئيسي كان موجودًا بالفعل بنفس الاسم — تم استخدامه بدل إنشاء نسخة جديدة.</p>
                }
                @if (r.subProjectsAlreadyLinked > 0) {
                  <p class="hint">{{ r.subProjectsAlreadyLinked }} مشروع فرعي كان موجودًا بالفعل ومرتبطًا بهذه السنة المالية — تم تخطيه دون تكرار.</p>
                }
              } @else {
                <p>معتمدة: {{ r.subProjectsApproved }} — جديدة ومعتمدة: {{ r.subProjectsCreatedAndApproved }}</p>
                @if (r.subProjectsAlreadyLinked > 0) {
                  <p class="hint">{{ r.subProjectsAlreadyLinked }} مشروع كان معتمدًا بالفعل من قبل — تم ربطه بهذه السنة المالية دون الحاجة لاعتماد جديد.</p>
                }
              }
              @if (r.failed.length > 0) {
                <div class="si-err">
                  <div class="si-err-title">فشل استيراد {{ r.failed.length }} صف:</div>
                  @for (f of r.failed; track f.name) {
                    <div class="si-err-row">
                      <span class="si-err-name">{{ f.name }}</span>
                      <span class="si-err-reason">{{ f.reason }}</span>
                    </div>
                  }
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
              <button class="si-btn primary" (click)="goToConfirm()">التالي</button>
            }
            @if (step() === 'confirm') {
              <button class="si-btn" (click)="step.set('reconcile')">رجوع</button>
              <button class="si-btn primary" [disabled]="committing()" (click)="submitCommit()">
                @if (committing()) { جاري الحفظ… } @else { تأكيد الاستيراد }
              </button>
            }
            @if (step() === 'result') {
              <button class="si-btn primary" (click)="finish()">تم</button>
            }
            <button class="si-btn" (click)="dismiss()">إلغاء</button>
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
    .recon-suggest { color: var(--warn); font-size: 12px; font-weight: 700; }
    .combo-wrap { position: relative; flex: 1; max-width: 320px; }
    .combo-wrap input { width: 100%; box-sizing: border-box; }
    .combo-wrap input.invalid { border-color: #b32a39; }
    .combo-list { position: absolute; top: calc(100% + 4px); right: 0; left: 0; max-height: 220px; overflow-y: auto; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; box-shadow: 0 8px 22px rgba(0,0,0,.16); z-index: 60; }
    .combo-item { display: block; width: 100%; text-align: right; padding: 8px 12px; font-size: 12.5px; background: none; border: none; border-bottom: 1px solid var(--line); cursor: pointer; color: inherit; }
    .combo-item:last-child { border-bottom: none; }
    .combo-item:hover { background: var(--surface-2); }
    .measure-rows { display: flex; flex-direction: column; gap: 8px; margin-top: 10px; width: 100%; padding: 10px; background: var(--surface-2); border-radius: 8px; }
    .measure-row { display: grid; grid-template-columns: 2fr 1fr 1.4fr auto; gap: 10px; align-items: end; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 9px 11px; }
    .measure-row .si-fld { margin: 0; }
    .measure-row .si-fld label { font-size: 11px; color: var(--muted); margin-bottom: 3px; display: block; }
    .si-x.sm { width: 32px; height: 32px; flex: 0 0 auto; }
  `],
})
export class ExcelImportWizard {
  private readonly importService = inject(ImportService);
  private readonly lookupsService = inject(LookupsService);
  private readonly agenciesService = inject(AgenciesService);
  private readonly projectsService = inject(ProjectsService);
  private readonly measurementsService = inject(MeasurementsService);

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
  protected readonly approvalDate = signal<string>(ExcelImportWizard.today());
  protected readonly existingOptions = signal<Record<string, Lookup[]>>({});
  protected readonly optionsLoading = signal(false);
  protected readonly subProjectOptions = signal<Lookup[]>([]);
  protected readonly subProjectOptionsLoading = signal(false);
  protected readonly allMeasurementNames = signal<string[]>([]);
  protected readonly allUnitNames = signal<string[]>([]);
  protected readonly openSubProjectDropdownRow = signal<number | null>(null);

  private readonly resolutions: Record<string, Map<string, ImportResolution>> = {
    markaz: new Map(), mainProgram: new Map(), subProgram: new Map(), agency: new Map(),
    projectLevel: new Map(), componentType: new Map(), accountingUnit: new Map(),
  };
  private readonly codeResolutions = new Map<string, MainProjectCodeResolution>();
  private readonly rowResolutions = new Map<number, ImportRowResolution>();
  private readonly measurementResolutions = new Map<number, ExtractedMeasurement[]>();
  private readonly subProjectLabelDrafts = new Map<number, string>();

  private wasOpen = false;
  private requestToken = 0;

  private static today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (isOpen && !this.wasOpen) {
        this.wasOpen = true;
        this.reset();
      } else if (!isOpen) {
        this.wasOpen = false;
      }
    });
  }

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

  protected existingOptionsFor(category: string): Lookup[] {
    return this.existingOptions()[category] ?? [];
  }

  protected isNew(category: string, name: string): boolean {
    return this.resolutions[category].get(name)?.createNew ?? true;
  }

  protected existingIdFor(category: string, name: string): number | null {
    return this.resolutions[category].get(name)?.existingId ?? null;
  }

  protected setNew(category: string, name: string): void {
    this.resolutions[category].set(name, { name, createNew: true, existingId: null });
  }

  protected setExisting(category: string, name: string, existingId: number | null): void {
    this.resolutions[category].set(name, { name, createNew: false, existingId });
  }

  protected isCodeOption(code: string, opt: { mainProjectName: string; mainProgramName: string }): boolean {
    const chosen = this.codeResolutions.get(code);
    return chosen?.chosenMainProjectName === opt.mainProjectName && chosen?.chosenMainProgramName === opt.mainProgramName;
  }

  protected hasCodeChoice(code: string): boolean {
    return this.codeResolutions.has(code);
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

  // Search-as-you-type combobox for "ربط بمشروع موجود" - a custom styled dropdown list (not a
  // native <select>/<datalist>, whose OS-default popup styling can't be themed) shown while the
  // input is focused, filtered against whatever the user is currently typing.
  protected subProjectLabelFor(rowIndex: number): string {
    if (this.subProjectLabelDrafts.has(rowIndex)) {
      return this.subProjectLabelDrafts.get(rowIndex)!;
    }
    const id = this.existingSubProjectId(rowIndex);
    if (id == null) return '';
    return this.subProjectOptions().find((o) => o.id === id)?.name ?? '';
  }

  protected filteredSubProjectOptions(rowIndex: number): Lookup[] {
    const query = this.subProjectLabelFor(rowIndex).trim().toLowerCase();
    const options = this.subProjectOptions();
    const filtered = query.length === 0 ? options : options.filter((o) => o.name.toLowerCase().includes(query));
    return filtered.slice(0, 20);
  }

  protected selectSubProjectOption(rowIndex: number, opt: Lookup): void {
    this.subProjectLabelDrafts.set(rowIndex, opt.name);
    this.setRowExisting(rowIndex, opt.id);
    this.openSubProjectDropdownRow.set(null);
  }

  protected onSubProjectLabelChange(rowIndex: number, label: string): void {
    this.subProjectLabelDrafts.set(rowIndex, label);
    // Typed text that doesn't exactly match a real option must NOT keep whatever id this row
    // resolved to before (e.g. a pre-filled AI suggestion) - that silent staleness is what let a
    // made-up name commit against an unrelated project instead of being caught by validation.
    const match = this.subProjectOptions().find((o) => o.name === label);
    this.setRowExisting(rowIndex, match ? match.id : null);
  }

  protected onSubProjectInputBlur(rowIndex: number): void {
    // mousedown on a dropdown option fires before blur, so a real selection already committed by
    // the time this runs; delay closing so that mousedown handler gets a chance to run first.
    setTimeout(() => {
      if (this.openSubProjectDropdownRow() === rowIndex) {
        this.openSubProjectDropdownRow.set(null);
      }
    }, 150);
  }

  protected isRowCreateNew(rowIndex: number): boolean {
    return this.rowResolutions.get(rowIndex)?.createNew ?? true;
  }

  protected pendingCreateNewCount(): number {
    return [...this.rowResolutions.values()].filter((r) => r.createNew).length;
  }

  protected pendingLinkExistingCount(): number {
    return [...this.rowResolutions.values()].filter((r) => !r.createNew).length;
  }

  protected measurementsForRow(rowIndex: number): ExtractedMeasurement[] {
    return this.measurementResolutions.get(rowIndex) ?? [];
  }

  protected updateMeasurement(rowIndex: number, index: number, field: keyof ExtractedMeasurement, value: string): void {
    const list = [...this.measurementsForRow(rowIndex)];
    const updated = { ...list[index] };
    if (field === 'value') {
      updated.value = Number(value) || 0;
    } else {
      (updated[field] as string) = value;
    }
    list[index] = updated;
    this.measurementResolutions.set(rowIndex, list);
  }

  protected removeMeasurement(rowIndex: number, index: number): void {
    const list = this.measurementsForRow(rowIndex).filter((_, i) => i !== index);
    this.measurementResolutions.set(rowIndex, list);
  }

  protected addMeasurement(rowIndex: number): void {
    const list = [...this.measurementsForRow(rowIndex), { name: '', value: 0, unit: '' }];
    this.measurementResolutions.set(rowIndex, list);
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  protected submitUpload(): void {
    const file = this.selectedFile();
    if (!file || this.uploading()) return;

    const token = ++this.requestToken;
    this.uploading.set(true);
    this.error.set(null);
    this.importService.preview(file, this.financialYearId()).subscribe({
      next: (result) => {
        if (token !== this.requestToken) return;
        this.uploading.set(false);
        this.preview.set(result);

        this.measurementResolutions.clear();
        for (const rowPreview of result.rowMeasurements) {
          this.measurementResolutions.set(rowPreview.rowIndex, [...rowPreview.measurements]);
        }

        for (const map of Object.values(this.resolutions)) map.clear();
        this.codeResolutions.clear();
        this.rowResolutions.clear();
        this.subProjectLabelDrafts.clear();
    this.openSubProjectDropdownRow.set(null);

        if (result.suggested) {
          for (const group of this.suggestedCategories(result.suggested)) {
            for (const item of group.items) {
              this.setNew(group.key, item.name);
            }
          }
          this.loadExistingOptions();
        }
        for (const row of result.approved?.unresolvedRows ?? []) {
          if (row.suggestedSubProjectId != null) {
            this.setRowExisting(row.rowIndex, row.suggestedSubProjectId);
          } else {
            this.setRowCreateNew(row.rowIndex);
          }
        }
        if ((result.approved?.unresolvedRows.length ?? 0) > 0) {
          this.loadSubProjectOptions();
        }
        this.step.set('reconcile');
      },
      error: (err) => {
        if (token !== this.requestToken) return;
        this.uploading.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر معالجة الملف');
      },
    });
  }

  private loadExistingOptions(): void {
    const token = this.requestToken;
    this.optionsLoading.set(true);
    forkJoin({
      markaz: this.lookupsService.getMarkaz(),
      mainProgram: this.lookupsService.getMainPrograms(),
      subProgram: this.lookupsService.getSubPrograms(),
      agency: this.agenciesService.getAll(),
      projectLevel: this.lookupsService.getProjectLevels(),
      componentType: this.lookupsService.getComponentTypes(),
      accountingUnit: this.lookupsService.getAccountingUnits(),
    }).subscribe({
      next: (result) => {
        if (token !== this.requestToken) return;
        this.optionsLoading.set(false);
        const mainProgramNameById = new Map(result.mainProgram.map((p) => [p.id, p.name]));
        const rawOptionsByCategory: Record<string, Lookup[]> = {
          markaz: result.markaz,
          mainProgram: result.mainProgram,
          subProgram: result.subProgram.map((sp) => ({ id: sp.id, name: sp.name })),
          agency: result.agency.map((a) => ({ id: a.id, name: a.agencyName })),
          projectLevel: result.projectLevel,
          componentType: result.componentType,
          accountingUnit: result.accountingUnit,
        };
        this.existingOptions.set({
          markaz: result.markaz,
          mainProgram: result.mainProgram,
          subProgram: result.subProgram.map((sp) => ({ id: sp.id, name: `${sp.name} (${mainProgramNameById.get(sp.mainProgramId) ?? ''})` })),
          agency: result.agency.map((a) => ({ id: a.id, name: a.agencyName })),
          projectLevel: result.projectLevel,
          componentType: result.componentType,
          accountingUnit: result.accountingUnit,
        });
        this.applySuggestedMatches(rawOptionsByCategory);
      },
      error: () => {
        if (token !== this.requestToken) return;
        this.optionsLoading.set(false);
        this.error.set('تعذّر تحميل القوائم الموجودة لخيار «ربط بموجود» — يمكنك المتابعة باستخدام «جديد» فقط');
      },
    });
  }

  // Pre-select "ربط بموجود" with the AI-suggested existing name, when the backend found one -
  // staff still see the "اقتراح" hint next to it and can switch back to "جديد" or a different
  // existing option before confirming; nothing is applied without them seeing it first.
  private applySuggestedMatches(rawOptionsByCategory: Record<string, Lookup[]>): void {
    const suggested = this.preview()?.suggested;
    if (!suggested) return;
    for (const group of this.suggestedCategories(suggested)) {
      const options = rawOptionsByCategory[group.key] ?? [];
      for (const item of group.items) {
        if (!item.suggestedMatch) continue;
        const match = options.find((o) => o.name === item.suggestedMatch);
        if (match) {
          this.setExisting(group.key, item.name, match.id);
        }
      }
    }
  }

  private loadSubProjectOptions(): void {
    const token = this.requestToken;
    this.subProjectOptionsLoading.set(true);
    // Scoped to the financial year this import targets - unscoped, this pulled sub-projects from
    // every year ever imported (1000+ after repeated test imports), which both pushed the actual
    // target (this year's own suggested-mode sub-project) past the pageSize cap - so the picker
    // couldn't resolve an id to a name and showed the placeholder instead of the suggestion - and
    // flooded manual search with same-named duplicates from unrelated years.
    this.projectsService.searchSubProjects({ page: 1, pageSize: 500, financialYearId: this.financialYearId() ?? undefined }).subscribe({
      next: (result) => {
        if (token !== this.requestToken) return;
        this.subProjectOptionsLoading.set(false);
        this.subProjectOptions.set(result.items.map((s) => ({ id: s.id, name: `${s.mainProjectName} / ${s.name}` })));
      },
      error: () => {
        if (token !== this.requestToken) return;
        this.subProjectOptionsLoading.set(false);
        this.error.set('تعذّر تحميل قائمة المشروعات الفرعية الموجودة لخيار «ربط بمشروع موجود» — يمكنك المتابعة باستخدام «إنشاء جديد» فقط');
      },
    });
  }

  private validateResolutions(): string | null {
    const preview = this.preview();
    if (!preview) return null;

    if (preview.mode === 'Suggested') {
      for (const map of Object.values(this.resolutions)) {
        for (const resolution of map.values()) {
          if (!resolution.createNew && resolution.existingId == null) {
            return 'برجاء اختيار القيمة الموجودة لكل عنصر تم اختيار «ربط بموجود» له';
          }
        }
      }
    } else {
      for (const resolution of this.rowResolutions.values()) {
        if (!resolution.createNew && resolution.existingSubProjectId == null) {
          return 'برجاء اختيار المشروع الفرعي الموجود لكل صف تم اختيار «ربط بمشروع موجود» له';
        }
      }
    }
    return null;
  }

  protected goToConfirm(): void {
    const validationError = this.validateResolutions();
    if (validationError) {
      this.error.set(validationError);
      return;
    }
    this.error.set(null);
    this.step.set('confirm');
  }

  protected submitCommit(): void {
    const preview = this.preview();
    const yearId = this.financialYearId();
    if (!preview || this.committing()) return;

    if (yearId == null) {
      this.error.set('برجاء اختيار سنة مالية أولاً');
      return;
    }

    if (preview.mode === 'Approved' && !this.approvalDate()) {
      this.error.set('برجاء إدخال تاريخ الاعتماد');
      return;
    }

    const validationError = this.validateResolutions();
    if (validationError) {
      this.error.set(validationError);
      return;
    }

    const token = ++this.requestToken;
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
      measurementResolutions: [...this.measurementResolutions.entries()].map(([rowIndex, measurements]) => ({ rowIndex, measurements })),
    };

    this.importService.commit(dto).subscribe({
      next: (result) => {
        if (token !== this.requestToken) return;
        this.committing.set(false);
        this.result.set(result);
        this.step.set('result');
      },
      error: (err) => {
        if (token !== this.requestToken) return;
        this.committing.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر إتمام الاستيراد');
      },
    });
  }

  protected finish(): void {
    this.reset();
    this.saved.emit();
  }

  // The × button, the overlay backdrop, and "إلغاء" are all generic "close this dialog" actions
  // that appear on every step, including the result screen after a successful commit. Without
  // this, closing via any of those instead of the explicit "تم" button skips the table reload
  // entirely - the import genuinely succeeded, but the Projects page keeps showing pre-import
  // data until a manual page refresh.
  protected dismiss(): void {
    if (this.result()) {
      this.finish();
    } else {
      this.close.emit();
    }
  }

  private reset(): void {
    this.requestToken++;
    this.step.set('upload');
    this.selectedFile.set(null);
    this.uploading.set(false);
    this.committing.set(false);
    this.preview.set(null);
    this.result.set(null);
    this.error.set(null);
    this.approvalDate.set(ExcelImportWizard.today());
    this.existingOptions.set({});
    this.optionsLoading.set(false);
    this.subProjectOptions.set([]);
    this.subProjectOptionsLoading.set(false);
    for (const map of Object.values(this.resolutions)) map.clear();
    this.codeResolutions.clear();
    this.rowResolutions.clear();
    this.measurementResolutions.clear();
    this.subProjectLabelDrafts.clear();
    this.openSubProjectDropdownRow.set(null);

    forkJoin({
      measurements: this.measurementsService.getAll(),
      units: this.lookupsService.getUnits(),
    }).subscribe({
      next: ({ measurements, units }) => {
        this.allMeasurementNames.set(measurements.map((m) => m.name));
        this.allUnitNames.set(units.map((u) => u.name));
      },
      error: () => {},
    });
  }
}
