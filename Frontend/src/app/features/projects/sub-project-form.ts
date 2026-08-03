import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { LookupsService } from '../../core/services/lookups.service';
import { AgenciesService } from '../../core/services/agencies.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { MeasurementsService } from '../../core/services/measurements.service';
import { MeasurementResolutionService } from '../../core/services/measurement-resolution.service';
import { AuthService } from '../../core/services/auth.service';
import {
  ExecutiveAgencyProfile,
  FinancialYear,
  Lookup,
  MainProjectListItem,
  MarkazLookup,
  Measurement,
  SetMeasurementValue,
  SubProgramLookup,
  SubProjectListItem,
} from '../../core/models/project.models';

export interface LockedParent {
  id: number;
  code: string | null;
  name: string;
}

interface MeasurementRow {
  name: string;
  value: number | null;
  unitName: string;
}

@Component({
  selector: 'app-sub-project-form',
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="si-overlay" (click)="close.emit()">
        <div class="si-modal" (click)="$event.stopPropagation()">
          <div class="si-modal-head">
            <div class="grow">
              <h3>{{ edit() ? 'تعديل مشروع فرعي' : locked() ? 'إضافة مشروع فرعي' : 'إضافة مشروع' }}</h3>
              <p>
                @if (edit()) {
                  إدخال الكود يعتمد المشروع تلقائيًا، وإزالته يعيده لمقترح
                } @else if (locked()) {
                  يُنشأ المشروع كمقترح ما لم يُدخَل كود
                } @else {
                  أنشئ مشروعًا رئيسيًا جديدًا مع أول مشروع فرعي، أو أضف مشروعًا فرعيًا لمشروع رئيسي قائم
                }
              </p>
            </div>
            <button class="si-x" (click)="close.emit()" aria-label="إغلاق">×</button>
          </div>

          <div class="si-modal-body">
            @if (error()) { <div class="si-err">{{ error() }}</div> }

            <div class="si-step"><span class="n">1</span><h4>المشروع الرئيسي</h4></div>

            @if (!locked() && !edit()) {
              <div class="seg mode-toggle">
                <button type="button" [class.on]="createMode() === 'existing'" (click)="setCreateMode('existing')">لمشروع قائم</button>
                <button type="button" [class.on]="createMode() === 'new'" (click)="setCreateMode('new')">مشروع جديد بالكامل</button>
              </div>
            }

            @if (locked() || edit()) {
              <div class="si-locked">
                <div class="lh">
                  <div><b>{{ currentMainDisplay()?.name }}</b><div class="lc">الكود: {{ currentMainDisplay()?.code ?? 'بانتظار الاعتماد' }}</div></div>
                  <span class="lb">🔒 المشروع الرئيسي التابع له</span>
                </div>
              </div>
            } @else if (createMode() === 'existing') {
              <div class="si-grid">
                <div class="si-fld full search-picker">
                  <label>المشروع الرئيسي <span class="req">*</span></label>
                  <input
                    [ngModel]="mainSearchTerm()"
                    (ngModelChange)="onMainSearchInput($event)"
                    (focus)="mainSearchOpen.set(true)"
                    (blur)="onMainSearchBlur()"
                    autocomplete="off"
                    placeholder="ابحث بالاسم أو الكود…"
                  />
                  @if (mainSearchOpen()) {
                    <div class="search-results">
                      @for (m of filteredMainOptions(); track m.id) {
                        <button type="button" (mousedown)="$event.preventDefault()" (click)="selectExistingMain(m)">
                          <span class="code">{{ m.code ?? 'مقترح' }}</span> {{ m.name }}
                        </button>
                      } @empty {
                        <div class="no-results">لا توجد نتائج</div>
                      }
                    </div>
                  }
                </div>
              </div>
            } @else {
              <div class="si-grid">
                <div class="si-fld">
                  <label>البرنامج الرئيسي <span class="req">*</span></label>
                  <select [ngModel]="newMainProgramId()" (ngModelChange)="onNewMainProgramChange($event)">
                    <option [ngValue]="null">— اختر —</option>
                    @for (p of mainPrograms(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                  </select>
                </div>
                <div class="si-fld">
                  <label>البرنامج الفرعي <span class="req">*</span></label>
                  <select [ngModel]="newMainSubProgramId()" (ngModelChange)="onNewMainSubProgramChange($event)">
                    <option [ngValue]="null">— اختر —</option>
                    @for (s of filteredNewMainSubPrograms(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
                  </select>
                </div>
                <div class="si-fld full">
                  <label>اسم المشروع الرئيسي <span class="req">*</span></label>
                  <input [ngModel]="newMainName()" (ngModelChange)="newMainName.set($event)" placeholder="مثال: تطوير شبكة الطرق الداخلية بشبين الكوم" />
                </div>
                <div class="si-fld full">
                  <label>كود المشروع الرئيسي (اختياري)</label>
                  <input [ngModel]="newMainCode()" (ngModelChange)="newMainCode.set($event)" placeholder="P-2627-XXX" />
                  <div class="hint">إدخال كود يعتمد المشروع تلقائيًا فور الحفظ؛ تركه فارغًا يبقيه بانتظار الاعتماد.</div>
                </div>
              </div>
            }

            <div class="si-step"><span class="n">2</span><h4>بيانات المشروع الفرعي</h4></div>
            <div class="si-grid">
              <div class="si-fld full">
                <label>اسم المشروع الفرعي <span class="req">*</span></label>
                <input [ngModel]="name()" (ngModelChange)="name.set($event)" placeholder="مثال: رصف طريق المحطة" />
              </div>
              <div class="si-fld full">
                <label>كود المشروع الفرعي (اختياري)</label>
                <input [ngModel]="code()" (ngModelChange)="code.set($event)" placeholder="SP-2627-XXX-A" />
                <div class="hint">إدخال كود يعتمد المشروع تلقائيًا فور الحفظ؛ تركه فارغًا يبقيه مقترحًا.</div>
              </div>
              <div class="si-fld">
                <label>المستوى <span class="req">*</span></label>
                <select [ngModel]="projectLevelId()" (ngModelChange)="projectLevelId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (pl of projectLevels(); track pl.id) { <option [ngValue]="pl.id">{{ pl.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>المكوّن العيني <span class="req">*</span></label>
                <select [ngModel]="componentTypeId()" (ngModelChange)="componentTypeId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (c of componentTypes(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>الوحدة الحسابية <span class="req">*</span></label>
                <select [ngModel]="accountingUnitId()" (ngModelChange)="accountingUnitId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (a of accountingUnits(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>المركز <span class="req">*</span></label>
                <select [ngModel]="markazId()" (ngModelChange)="markazId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (mk of markazList(); track mk.id) { <option [ngValue]="mk.id">{{ mk.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>الأولوية <span class="req">*</span></label>
                <select [ngModel]="priorityId()" (ngModelChange)="priorityId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (p of priorities(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>حالة المشروع <span class="req">*</span></label>
                <select [ngModel]="statusId()" (ngModelChange)="statusId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (st of statuses(); track st.id) { <option [ngValue]="st.id">{{ st.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>جهة التنفيذ</label>
                <select [ngModel]="executiveAgencyId()" (ngModelChange)="executiveAgencyId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (a of executiveAgencies(); track a.id) { <option [ngValue]="a.id">{{ a.agencyName }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>تمويل بنكي (ج.م)</label>
                <input type="number" [ngModel]="bankFunding()" (ngModelChange)="bankFunding.set($event)" placeholder="0" />
              </div>
              <div class="si-fld">
                <label>تمويل ذاتي (ج.م)</label>
                <input type="number" [ngModel]="selfFunding()" (ngModelChange)="selfFunding.set($event)" placeholder="0" />
              </div>
              <div class="si-fld full">
                <label>ملاحظات / وصف</label>
                <textarea [ngModel]="description()" (ngModelChange)="description.set($event)" placeholder="أي ملاحظات إضافية…"></textarea>
              </div>
            </div>

            <div class="si-step"><span class="n">3</span><h4>السنوات المالية</h4></div>
            <div class="si-years">
              @for (y of financialYears(); track y.id) {
                <label class="si-year-chk">
                  <input type="checkbox" [checked]="checkedYearIds().has(y.id)" (change)="toggleYear(y.id)" />
                  {{ y.name }}
                </label>
              } @empty {
                <p class="hint">لا توجد سنوات مالية بعد.</p>
              }
            </div>

            <div class="si-step"><span class="n">4</span><h4>القياسات المخصصة</h4></div>
            @if (effectiveSubProgramId() == null) {
              <p class="hint">
                @if (!locked() && !edit() && createMode() === 'new') {
                  اختر البرنامج الفرعي أعلاه أولًا لإضافة القياسات المخصصة.
                } @else {
                  اختر المشروع الرئيسي أعلاه أولًا لإضافة القياسات المخصصة.
                }
              </p>
            } @else {
              <div class="measure-rows">
                @for (row of measurementRows(); track $index; let i = $index) {
                  <div class="measure-row">
                    <div class="si-fld">
                      <label>اسم القياس</label>
                      <input
                        list="measurement-names-list"
                        [ngModel]="row.name"
                        (ngModelChange)="updateMeasurementRow(i, 'name', $event)"
                        placeholder="مثال: عدد"
                      />
                    </div>
                    <div class="si-fld">
                      <label>القيمة</label>
                      <input
                        type="number"
                        [ngModel]="row.value"
                        (ngModelChange)="updateMeasurementRow(i, 'value', $event)"
                        placeholder="مثال: 3"
                      />
                    </div>
                    <div class="si-fld">
                      <label>الوحدة</label>
                      <input
                        list="measurement-units-list"
                        [ngModel]="row.unitName"
                        (ngModelChange)="updateMeasurementRow(i, 'unitName', $event)"
                        placeholder="مثال: سيارة 50 طن"
                      />
                    </div>
                    <button type="button" class="si-x sm" (click)="removeMeasurementRow(i)" aria-label="حذف القياس">×</button>
                  </div>
                } @empty {
                  <p class="hint">لا توجد قياسات بعد.</p>
                }
                <datalist id="measurement-names-list">
                  @for (m of applicableMeasurements(); track m.id) { <option [value]="m.name"></option> }
                </datalist>
                <datalist id="measurement-units-list">
                  @for (u of allUnits(); track u.id) { <option [value]="u.name"></option> }
                </datalist>
                <button type="button" class="si-btn" (click)="addMeasurementRow()">
                  <svg viewBox="0 0 24 24" width="15" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
                  إضافة قياس
                </button>
              </div>
            }
          </div>

          <div class="si-modal-foot">
            <button class="si-btn primary" [disabled]="saving()" (click)="submit()">
              @if (saving()) { <span class="mini-sp"></span> جاري الحفظ… } @else { {{ edit() ? 'حفظ التعديلات' : 'إضافة المشروع' }} }
            </button>
            @if (edit() && isManager()) {
              <button class="si-btn danger" type="button" [disabled]="saving()" (click)="onDelete()">حذف المشروع</button>
            }
            <button class="si-btn" (click)="close.emit()">إلغاء</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .mini-sp{width:14px;height:14px;border:2px solid rgba(255,255,255,.4);border-top-color:#fff;border-radius:50%;animation:spin .7s linear infinite;display:inline-block}
    @keyframes spin{to{transform:rotate(360deg)}}
    .si-years{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:16px}
    .si-year-chk{display:flex;align-items:center;gap:7px;border:1px solid var(--line-strong);border-radius:9px;padding:8px 12px;font-size:13px;font-weight:700;background:var(--surface)}
    .si-years .hint{font-size:12px;color:var(--muted)}
    .mode-toggle{display:flex;background:var(--surface);border:1px solid var(--line);border-radius:var(--radius-sm);padding:4px;box-shadow:var(--shadow-xs);margin-bottom:14px;width:fit-content}
    .mode-toggle button{border:0;background:transparent;color:var(--muted);padding:9px 15px;border-radius:7px;font-weight:700;font-size:12.5px;white-space:nowrap;cursor:pointer}
    .mode-toggle button.on{background:linear-gradient(155deg,var(--green-600),var(--green-800));color:#fff}
    .search-picker{position:relative}
    .search-results{position:absolute;top:100%;inset-inline:0;z-index:20;margin-top:4px;max-height:220px;overflow-y:auto;background:var(--surface);border:1px solid var(--line-strong);border-radius:var(--radius-sm);box-shadow:var(--shadow-sm)}
    .search-results button{display:block;width:100%;text-align:start;padding:9px 12px;border:0;background:transparent;font-size:13px;font-weight:600;color:var(--ink);cursor:pointer}
    .search-results button:hover{background:var(--surface-2)}
    .search-results button .code{font-weight:800;color:var(--green-700);margin-inline-end:6px}
    .search-results .no-results{padding:10px 12px;font-size:12px;color:var(--muted)}
    .measure-rows{display:flex;flex-direction:column;gap:10px;margin-bottom:14px}
    .measure-row{display:grid;grid-template-columns:1fr 1fr 1fr auto;gap:10px;align-items:end}
    .measure-row .si-fld{margin:0}
    .si-x.sm{width:32px;height:32px;flex:0 0 auto}
  `],
})
export class SubProjectForm {
  private readonly projectsService = inject(ProjectsService);
  private readonly lookups = inject(LookupsService);
  private readonly agenciesService = inject(AgenciesService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly measurementsService = inject(MeasurementsService);
  private readonly measurementResolution = inject(MeasurementResolutionService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  readonly open = input(false);
  readonly edit = input<SubProjectListItem | null>(null);
  readonly locked = input<LockedParent | null>(null);
  readonly mains = input<MainProjectListItem[]>([]);
  readonly defaultYearId = input<number | null>(null);
  readonly close = output<void>();
  readonly saved = output<void>();
  readonly delete = output<void>();

  protected readonly priorities = signal<Lookup[]>([]);
  protected readonly statuses = signal<Lookup[]>([]);
  protected readonly markazList = signal<MarkazLookup[]>([]);
  protected readonly projectLevels = signal<Lookup[]>([]);
  protected readonly componentTypes = signal<Lookup[]>([]);
  protected readonly accountingUnits = signal<Lookup[]>([]);
  protected readonly mainPrograms = signal<Lookup[]>([]);
  protected readonly subPrograms = signal<SubProgramLookup[]>([]);
  protected readonly executiveAgencies = signal<ExecutiveAgencyProfile[]>([]);
  protected readonly allUnits = signal<Lookup[]>([]);

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly checkedYearIds = signal<Set<number>>(new Set());
  private originalYearIds = new Set<number>();

  protected readonly mainProjectId = signal<number | null>(null);
  protected readonly code = signal('');
  protected readonly name = signal('');
  protected readonly projectLevelId = signal<number | null>(null);
  protected readonly componentTypeId = signal<number | null>(null);
  protected readonly accountingUnitId = signal<number | null>(null);
  protected readonly markazId = signal<number | null>(null);
  protected readonly priorityId = signal<number | null>(null);
  protected readonly statusId = signal<number | null>(null);
  protected readonly bankFunding = signal<number>(0);
  protected readonly selfFunding = signal<number>(0);
  protected readonly description = signal('');
  protected readonly executiveAgencyId = signal<number | null>(null);
  private originalExecutiveAgencyId: number | null = null;

  // ===== وضع الإضافة: لمشروع قائم / مشروع جديد بالكامل =====
  protected readonly createMode = signal<'existing' | 'new'>('existing');

  protected readonly mainSearchTerm = signal('');
  protected readonly mainSearchOpen = signal(false);
  protected readonly filteredMainOptions = computed(() => {
    const term = this.mainSearchTerm().trim().toLowerCase();
    const all = this.mains();
    if (!term) return all.slice(0, 20);
    return all
      .filter((m) => m.name.toLowerCase().includes(term) || (m.code ?? '').toLowerCase().includes(term))
      .slice(0, 20);
  });

  protected readonly newMainProgramId = signal<number | null>(null);
  protected readonly newMainSubProgramId = signal<number | null>(null);
  protected readonly newMainName = signal('');
  protected readonly newMainCode = signal('');
  protected readonly filteredNewMainSubPrograms = computed(() => {
    const pid = this.newMainProgramId();
    return this.subPrograms().filter((s) => pid == null || s.mainProgramId === pid);
  });

  protected readonly currentMainDisplay = computed<LockedParent | null>(() => {
    if (this.locked()) return this.locked();
    const id = this.mainProjectId();
    const m = this.mains().find((x) => x.id === id);
    return m ? { id: m.id, code: m.code, name: m.name } : null;
  });

  protected readonly effectiveSubProgramId = computed<number | null>(() => {
    if (!this.locked() && !this.edit() && this.createMode() === 'new') {
      return this.newMainSubProgramId();
    }
    const id = this.mainProjectId();
    return id == null ? null : this.subProgramIdForMain(id);
  });

  protected readonly applicableMeasurements = signal<Measurement[]>([]);
  protected readonly measurementRows = signal<MeasurementRow[]>([]);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  private lookupsLoaded = false;
  private wasOpen = false;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (isOpen && !this.wasOpen) {
        this.wasOpen = true;
        this.onOpen();
      } else if (!isOpen) {
        this.wasOpen = false;
      }
    });
  }

  private onOpen(): void {
    this.error.set(null);
    this.ensureLookups(() => this.prefill());
  }

  private ensureLookups(done: () => void): void {
    if (this.lookupsLoaded) {
      done();
      return;
    }
    forkJoin({
      priorities: this.lookups.getPriorities(),
      statuses: this.lookups.getStatuses(),
      markaz: this.lookups.getMarkaz(),
      projectLevels: this.lookups.getProjectLevels(),
      componentTypes: this.lookups.getComponentTypes(),
      accountingUnits: this.lookups.getAccountingUnits(),
      financialYears: this.financialYearsService.getAll(),
      mainPrograms: this.lookups.getMainPrograms(),
      subPrograms: this.lookups.getSubPrograms(),
      agencies: this.agenciesService.getAll(),
      units: this.lookups.getUnits(),
    }).subscribe({
      next: ({ priorities, statuses, markaz, projectLevels, componentTypes, accountingUnits, financialYears, mainPrograms, subPrograms, agencies, units }) => {
        this.priorities.set(priorities);
        this.statuses.set(statuses);
        this.markazList.set(markaz);
        this.projectLevels.set(projectLevels);
        this.componentTypes.set(componentTypes);
        this.accountingUnits.set(accountingUnits);
        this.financialYears.set(financialYears);
        this.mainPrograms.set(mainPrograms);
        this.subPrograms.set(subPrograms);
        this.executiveAgencies.set(agencies);
        this.allUnits.set(units);
        this.lookupsLoaded = true;
        done();
      },
      error: () => this.error.set('تعذّر تحميل القوائم'),
    });
  }

  private subProgramIdForMain(mainProjectId: number): number | null {
    return this.mains().find((m) => m.id === mainProjectId)?.subProgramId ?? null;
  }

  private prefill(): void {
    this.resetForm();
    const e = this.edit();
    const lockedParent = this.locked();

    if (lockedParent) {
      this.mainProjectId.set(lockedParent.id);
      const subProgramId = this.subProgramIdForMain(lockedParent.id);
      if (subProgramId != null) this.loadApplicableMeasurementsForSubProgram(subProgramId);
    }

    if (e) {
      this.projectsService.getSubProject(e.id).subscribe({
        next: (d) => {
          this.mainProjectId.set(d.mainProjectId);
          this.code.set(d.code ?? '');
          this.name.set(d.name);
          this.projectLevelId.set(d.projectLevelId);
          this.componentTypeId.set(d.componentTypeId);
          this.accountingUnitId.set(d.accountingUnitId);
          this.markazId.set(d.markazId);
          this.priorityId.set(d.priorityId);
          this.statusId.set(d.statusId);
          this.bankFunding.set(d.bankFunding);
          this.selfFunding.set(d.selfFunding);
          this.description.set(d.description ?? '');
          this.executiveAgencyId.set(d.executiveAgencyId);
          this.originalExecutiveAgencyId = d.executiveAgencyId;
          const subProgramId = this.subProgramIdForMain(d.mainProjectId);
          if (subProgramId != null) this.loadApplicableMeasurementsForSubProgram(subProgramId, e.id);
        },
        error: () => this.error.set('تعذّر تحميل بيانات المشروع الفرعي'),
      });

      this.projectsService.getSubProjectFinancialYears(e.id).subscribe({
        next: (links) => {
          const ids = new Set(links.map((l) => l.financialYearId));
          this.originalYearIds = ids;
          this.checkedYearIds.set(new Set(ids));
        },
        error: () => this.error.set('تعذّر تحميل السنوات المالية المرتبطة بهذا المشروع'),
      });
    } else {
      this.originalYearIds = new Set<number>();
      const defaultId = this.defaultYearId();
      this.checkedYearIds.set(defaultId != null ? new Set([defaultId]) : new Set<number>());
    }
  }

  protected setCreateMode(mode: 'existing' | 'new'): void {
    this.createMode.set(mode);
    this.measurementRows.set([]);
    this.applicableMeasurements.set([]);
    if (mode === 'new') {
      this.mainProjectId.set(null);
      this.mainSearchTerm.set('');
      this.mainSearchOpen.set(false);
    } else {
      this.newMainProgramId.set(null);
      this.newMainSubProgramId.set(null);
      this.newMainName.set('');
      this.newMainCode.set('');
    }
  }

  protected onMainSearchInput(value: string): void {
    this.mainSearchTerm.set(value);
    this.mainSearchOpen.set(true);
    if (this.mainProjectId() != null) {
      this.mainProjectId.set(null);
      this.applicableMeasurements.set([]);
      this.measurementRows.set([]);
    }
  }

  protected onMainSearchBlur(): void {
    setTimeout(() => this.mainSearchOpen.set(false), 150);
  }

  protected selectExistingMain(m: MainProjectListItem): void {
    this.mainProjectId.set(m.id);
    this.mainSearchTerm.set(`${m.code ?? 'مقترح'} — ${m.name}`);
    this.mainSearchOpen.set(false);
    this.loadApplicableMeasurementsForSubProgram(m.subProgramId);
  }

  protected onNewMainProgramChange(id: number | null): void {
    this.newMainProgramId.set(id);
    this.newMainSubProgramId.set(null);
    this.applicableMeasurements.set([]);
    this.measurementRows.set([]);
  }

  protected onNewMainSubProgramChange(id: number | null): void {
    this.newMainSubProgramId.set(id);
    if (id != null) {
      this.loadApplicableMeasurementsForSubProgram(id);
    } else {
      this.applicableMeasurements.set([]);
      this.measurementRows.set([]);
    }
  }

  private loadApplicableMeasurementsForSubProgram(subProgramId: number, subProjectId?: number): void {
    this.measurementsService.getApplicable(subProgramId).subscribe({
      next: (measurements) => {
        this.applicableMeasurements.set(measurements);
        if (subProjectId != null) {
          this.measurementsService.getValuesForSubProject(subProjectId).subscribe({
            next: (values) => {
              this.measurementRows.set(
                values.map((v) => ({ name: v.measurementName, value: v.value, unitName: v.unitName ?? '' })),
              );
            },
            error: () => {},
          });
        } else {
          this.measurementRows.set([]);
        }
      },
      error: () => this.applicableMeasurements.set([]),
    });
  }

  protected addMeasurementRow(): void {
    this.measurementRows.update((rows) => [...rows, { name: '', value: null, unitName: '' }]);
  }

  protected removeMeasurementRow(i: number): void {
    this.measurementRows.update((rows) => rows.filter((_, idx) => idx !== i));
  }

  protected updateMeasurementRow(i: number, field: 'name' | 'value' | 'unitName', value: string): void {
    const rows = [...this.measurementRows()];
    const row = { ...rows[i] };
    if (field === 'value') {
      row.value = value === '' || value == null ? null : Number(value);
    } else {
      row[field] = value;
    }
    rows[i] = row;
    this.measurementRows.set(rows);
  }

  private resetForm(): void {
    this.mainProjectId.set(null);
    this.code.set('');
    this.name.set('');
    this.projectLevelId.set(null);
    this.componentTypeId.set(null);
    this.accountingUnitId.set(null);
    this.markazId.set(null);
    this.priorityId.set(null);
    this.statusId.set(null);
    this.bankFunding.set(0);
    this.selfFunding.set(0);
    this.description.set('');
    this.executiveAgencyId.set(null);
    this.originalExecutiveAgencyId = null;
    this.checkedYearIds.set(new Set());
    this.originalYearIds = new Set<number>();
    this.applicableMeasurements.set([]);
    this.measurementRows.set([]);
    this.createMode.set('existing');
    this.mainSearchTerm.set('');
    this.mainSearchOpen.set(false);
    this.newMainProgramId.set(null);
    this.newMainSubProgramId.set(null);
    this.newMainName.set('');
    this.newMainCode.set('');
  }

  protected onDelete(): void {
    this.delete.emit();
  }

  protected toggleYear(id: number): void {
    this.checkedYearIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected submit(): void {
    if (this.saving()) return;
    this.error.set(null);

    const creatingNewMain = !this.locked() && !this.edit() && this.createMode() === 'new';

    if (creatingNewMain) {
      if (this.newMainSubProgramId() == null) { this.error.set('برجاء اختيار البرنامج الفرعي'); return; }
      if (!this.newMainName().trim()) { this.error.set('برجاء إدخال اسم المشروع الرئيسي'); return; }
    } else if (!this.edit() && this.mainProjectId() == null) {
      this.error.set('برجاء اختيار المشروع الرئيسي');
      return;
    }

    if (!this.name().trim()) { this.error.set('برجاء إدخال اسم المشروع الفرعي'); return; }
    if (this.projectLevelId() == null) { this.error.set('برجاء اختيار المستوى'); return; }
    if (this.componentTypeId() == null) { this.error.set('برجاء اختيار المكوّن العيني'); return; }
    if (this.accountingUnitId() == null) { this.error.set('برجاء اختيار الوحدة الحسابية'); return; }
    if (this.markazId() == null) { this.error.set('برجاء اختيار المركز'); return; }
    if (this.priorityId() == null) { this.error.set('برجاء اختيار الأولوية'); return; }
    if (this.statusId() == null) { this.error.set('برجاء اختيار حالة المشروع'); return; }

    this.saving.set(true);

    if (creatingNewMain) {
      this.projectsService
        .createMainProject({
          code: this.newMainCode().trim() || null,
          name: this.newMainName().trim(),
          executingAgency: '',
          subProgramId: this.newMainSubProgramId()!,
        })
        .subscribe({
          next: (main) => this.submitSubProject(main.id),
          error: (err) => {
            this.saving.set(false);
            this.error.set(err?.error?.message ?? 'تعذّر إنشاء المشروع الرئيسي');
          },
        });
    } else {
      this.submitSubProject(this.mainProjectId() ?? undefined);
    }
  }

  private submitSubProject(mainProjectId?: number): void {
    const base = {
      code: this.code().trim() || null,
      name: this.name().trim(),
      projectLevelId: this.projectLevelId()!,
      componentTypeId: this.componentTypeId()!,
      accountingUnitId: this.accountingUnitId()!,
      projectNature: '',
      markazId: this.markazId()!,
      priorityId: this.priorityId()!,
      statusId: this.statusId()!,
      bankFunding: Number(this.bankFunding()) || 0,
      selfFunding: Number(this.selfFunding()) || 0,
      latitude: null,
      longitude: null,
      description: this.description().trim() || null,
    };

    const editing = this.edit();
    const req = editing
      ? this.projectsService.updateSubProject(editing.id, base)
      : this.projectsService.createSubProject({ ...base, mainProjectId: (mainProjectId ?? this.mainProjectId())! });

    req.subscribe({
      next: (result) => {
        const subProjectId = editing ? editing.id : result.id;
        this.syncExecutiveAgency(subProjectId);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر حفظ المشروع الفرعي');
      },
    });
  }

  private syncExecutiveAgency(subProjectId: number): void {
    const agencyId = this.executiveAgencyId();
    if (agencyId == null || agencyId === this.originalExecutiveAgencyId) {
      this.syncFinancialYears(subProjectId);
      return;
    }

    this.projectsService.assignExecutiveAgency(subProjectId, agencyId).subscribe({
      next: () => this.syncFinancialYears(subProjectId),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر تعيين جهة التنفيذ');
      },
    });
  }

  private syncFinancialYears(subProjectId: number): void {
    const desired = this.checkedYearIds();
    const toLink = [...desired].filter((id) => !this.originalYearIds.has(id));
    const toUnlink = [...this.originalYearIds].filter((id) => !desired.has(id));
    const calls = [
      ...toLink.map((id) => this.projectsService.linkFinancialYear(subProjectId, id)),
      ...toUnlink.map((id) => this.projectsService.unlinkFinancialYear(subProjectId, id)),
    ];

    if (calls.length === 0) {
      this.syncMeasurementValues(subProjectId);
      return;
    }

    forkJoin(calls).subscribe({
      next: () => this.syncMeasurementValues(subProjectId),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر تحديث ربط السنوات المالية');
      },
    });
  }

  private async syncMeasurementValues(subProjectId: number): Promise<void> {
    const rows = this.measurementRows().filter((r) => r.name.trim() && r.unitName.trim());
    const subProgramId = this.effectiveSubProgramId();

    if (rows.length === 0 || subProgramId == null) {
      this.saving.set(false);
      this.saved.emit();
      return;
    }

    try {
      const values = await this.resolveMeasurementRows(rows, subProgramId);
      this.measurementsService.setValuesForSubProject(subProjectId, values).subscribe({
        next: () => {
          this.saving.set(false);
          this.saved.emit();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.message ?? 'تعذّر حفظ القياسات');
        },
      });
    } catch (err: unknown) {
      this.saving.set(false);
      const httpErr = err as { error?: { message?: string } };
      this.error.set(httpErr?.error?.message ?? 'تعذّر معالجة القياسات');
    }
  }

  private async resolveMeasurementRows(rows: MeasurementRow[], subProgramId: number): Promise<SetMeasurementValue[]> {
    const result = await this.measurementResolution.resolveRows(
      rows,
      subProgramId,
      this.applicableMeasurements(),
      this.allUnits(),
    );
    this.applicableMeasurements.set(result.measurements);
    this.allUnits.set(result.units);
    return result.values;
  }
}
