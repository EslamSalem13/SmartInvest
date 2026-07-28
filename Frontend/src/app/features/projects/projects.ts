import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { LookupsService } from '../../core/services/lookups.service';
import { AuthService } from '../../core/services/auth.service';
import {
  EXECUTING_AGENCIES,
  Lookup,
  MainProjectListItem,
  MarkazLookup,
  SubProgramLookup,
  SubProjectListItem,
} from '../../core/models/project.models';
import { MainProjectForm } from './main-project-form';
import { SubProjectForm, LockedParent } from './sub-project-form';

interface MainWithSubs {
  main: MainProjectListItem;
  subs: SubProjectListItem[];
}

interface ProgramGroup {
  key: string;
  label: string;
  mains: MainWithSubs[];
}

@Component({
  selector: 'app-projects',
  imports: [FormsModule, RouterLink, MainProjectForm, SubProjectForm],
  templateUrl: './projects.html',
  styleUrl: './projects.css',
})
export class Projects {
  private readonly projectsService = inject(ProjectsService);
  private readonly lookups = inject(LookupsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;
  protected readonly agencies = EXECUTING_AGENCIES;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly mains = signal<MainProjectListItem[]>([]);
  protected readonly subs = signal<SubProjectListItem[]>([]);

  // ابحث + فلاتر
  protected readonly searchTerm = signal('');
  protected readonly approvalFilter = signal<'all' | 'approved' | 'pending'>('all');
  protected readonly showAdvanced = signal(false);
  protected readonly fMainProgram = signal('');
  protected readonly fSubProgram = signal('');
  protected readonly fLevel = signal('');
  protected readonly fAgency = signal('');
  protected readonly fMarkaz = signal('');
  protected readonly fPriority = signal('');
  protected readonly fFunding = signal('');

  // قوائم الفلاتر
  protected readonly mainPrograms = signal<Lookup[]>([]);
  protected readonly subPrograms = signal<SubProgramLookup[]>([]);
  protected readonly markazList = signal<MarkazLookup[]>([]);
  protected readonly priorities = signal<Lookup[]>([]);

  // كاش جهة التنفيذ لكل مشروع رئيسي (لعرضها في صفوف الفرعي)
  private readonly agencyByMain = computed(() => {
    const map = new Map<number, string>();
    for (const m of this.mains()) {
      map.set(m.id, m.executingAgency);
    }
    return map;
  });

  agencyOf(mainId: number): string {
    return this.agencyByMain().get(mainId) ?? '';
  }

  // ===== مؤشرات =====
  protected readonly kpiTotal = computed(() => this.subs().length);
  protected readonly kpiApproved = computed(() => this.subs().filter((s) => s.isApproved).length);
  protected readonly kpiPending = computed(() => this.subs().filter((s) => !s.isApproved).length);
  protected readonly kpiBank = computed(() => this.subs().reduce((a, s) => a + s.bankFunding, 0));
  protected readonly kpiSelf = computed(() => this.subs().reduce((a, s) => a + s.selfFunding, 0));

  // ===== الفلترة والتجميع =====
  private matchesSubFilters(s: SubProjectListItem): boolean {
    if (this.approvalFilter() === 'approved' && !s.isApproved) return false;
    if (this.approvalFilter() === 'pending' && s.isApproved) return false;
    if (this.fLevel() && s.projectLevel !== this.fLevel()) return false;
    if (this.fMarkaz() && String(s.markazId) !== this.fMarkaz()) return false;
    if (this.fPriority() && String(s.priorityId) !== this.fPriority()) return false;
    if (this.fFunding() === 'bank' && s.bankFunding <= 0) return false;
    if (this.fFunding() === 'self' && s.selfFunding <= 0) return false;
    return true;
  }

  private matchesMainFilters(m: MainProjectListItem): boolean {
    if (this.fMainProgram() && m.mainProgramName !== this.fMainProgram()) return false;
    if (this.fSubProgram() && m.subProgramName !== this.fSubProgram()) return false;
    if (this.fAgency() && m.executingAgency !== this.fAgency()) return false;
    return true;
  }

  // ===== التجميع: برنامج ← مشروع رئيسي ← فرعيات =====
  protected readonly filteredGroups = computed<ProgramGroup[]>(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const subsByMain = new Map<number, SubProjectListItem[]>();
    for (const s of this.subs()) {
      const arr = subsByMain.get(s.mainProjectId) ?? [];
      arr.push(s);
      subsByMain.set(s.mainProjectId, arr);
    }

    const mains = [...this.mains()].sort((a, b) => {
      const la = `${a.mainProgramName} ${a.subProgramName}`;
      const lb = `${b.mainProgramName} ${b.subProgramName}`;
      return la.localeCompare(lb, 'ar') || a.name.localeCompare(b.name, 'ar');
    });

    const groupMap = new Map<string, ProgramGroup>();
    for (const m of mains) {
      if (!this.matchesMainFilters(m)) continue;

      let mySubs = (subsByMain.get(m.id) ?? []).filter((s) => this.matchesSubFilters(s));

      if (term) {
        const mainHit =
          m.name.toLowerCase().includes(term) || (m.code ?? '').toLowerCase().includes(term);
        const subHits = mySubs.filter(
          (s) => s.name.toLowerCase().includes(term) || (s.code ?? '').toLowerCase().includes(term),
        );
        if (!mainHit && subHits.length === 0) continue;
        if (!mainHit) mySubs = subHits;
      }

      const key = `${m.mainProgramName} ← ${m.subProgramName}`;
      if (!groupMap.has(key)) {
        groupMap.set(key, { key, label: key, mains: [] });
      }
      groupMap.get(key)!.mains.push({ main: m, subs: mySubs });
    }

    return [...groupMap.values()];
  });

  // ===== الطي والفتح (accordion) — حالة مستقلة لكل مستوى =====
  protected readonly expandedPrograms = signal<Set<string | number>>(new Set());
  protected readonly expandedProjects = signal<Set<number>>(new Set());

  protected toggleProgram(key: string | number): void {
    const next = new Set(this.expandedPrograms());
    if (next.has(key)) {
      next.delete(key);
    } else {
      next.add(key);
    }
    this.expandedPrograms.set(next);
  }

  protected isProgramExpanded(key: string | number): boolean {
    return this.expandedPrograms().has(key);
  }

  protected toggleProject(projectId: number): void {
    const next = new Set(this.expandedProjects());
    if (next.has(projectId)) {
      next.delete(projectId);
    } else {
      next.add(projectId);
    }
    this.expandedProjects.set(next);
  }

  protected isProjectExpanded(projectId: number): boolean {
    return this.expandedProjects().has(projectId);
  }

  // ===== pagination على البرامج =====
  protected readonly page = signal(1);
  protected readonly pageSize = 4;
  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredGroups().length / this.pageSize)),
  );
  protected readonly pagedGroups = computed<ProgramGroup[]>(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filteredGroups().slice(start, start + this.pageSize);
  });
  protected readonly rangeStart = computed(() =>
    this.filteredGroups().length === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly rangeEnd = computed(() =>
    Math.min(this.page() * this.pageSize, this.filteredGroups().length),
  );
  // إجماليات المعروض (لكل البرامج في الصفحة الحالية)
  protected readonly shownBank = computed(() =>
    this.pagedGroups().reduce(
      (a, g) => a + g.mains.reduce((x, m) => x + m.subs.reduce((y, s) => y + s.bankFunding, 0), 0),
      0,
    ),
  );
  protected readonly shownSelf = computed(() =>
    this.pagedGroups().reduce(
      (a, g) => a + g.mains.reduce((x, m) => x + m.subs.reduce((y, s) => y + s.selfFunding, 0), 0),
      0,
    ),
  );

  protected goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) this.page.set(p);
  }

  // ===== modals =====
  protected readonly showMainForm = signal(false);
  protected readonly mainEdit = signal<MainProjectListItem | null>(null);
  protected readonly showSubForm = signal(false);
  protected readonly subEdit = signal<SubProjectListItem | null>(null);
  protected readonly subLocked = signal<LockedParent | null>(null);
  protected readonly addMenuOpen = signal(false);

  constructor() {
    this.load();
    this.loadLookups();
    effect(() => {
      this.searchTerm();
      this.approvalFilter();
      this.fMainProgram(); this.fSubProgram(); this.fLevel();
      this.fAgency(); this.fMarkaz(); this.fPriority(); this.fFunding();
      this.page.set(1);
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      mains: this.projectsService.getMainProjects(),
      subs: this.projectsService.searchSubProjects({ page: 1, pageSize: 1000 }),
    }).subscribe({
      next: ({ mains, subs }) => {
        this.mains.set(mains);
        this.subs.set(subs.items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل المشروعات. تأكد من تشغيل الخادم وتسجيل الدخول.');
        this.loading.set(false);
      },
    });
  }

  private loadLookups(): void {
    this.lookups.getMainPrograms().subscribe((d) => this.mainPrograms.set(d));
    this.lookups.getSubPrograms().subscribe((d) => this.subPrograms.set(d));
    this.lookups.getMarkaz().subscribe((d) => this.markazList.set(d));
    this.lookups.getPriorities().subscribe((d) => this.priorities.set(d));
  }

  protected clearFilters(): void {
    this.fMainProgram.set(''); this.fSubProgram.set(''); this.fLevel.set('');
    this.fAgency.set(''); this.fMarkaz.set(''); this.fPriority.set(''); this.fFunding.set('');
    this.searchTerm.set(''); this.approvalFilter.set('all');
  }

  protected money(value: number): string {
    return (value ?? 0).toLocaleString('en-US');
  }

  // صيغة عربية صحيحة لعدد المشاريع الرئيسية (مفرد/مثنى/جمع)
  protected mainCountLabel(n: number): string {
    if (n === 1) return 'مشروع رئيسي واحد';
    if (n === 2) return 'مشروعان رئيسيان';
    if (n >= 3 && n <= 10) return `${n} مشاريع رئيسية`;
    return `${n} مشروعًا رئيسيًا`;
  }

  // ===== modals actions =====
  protected toggleAddMenu(): void { this.addMenuOpen.set(!this.addMenuOpen()); }
  protected openAddMain(): void { this.addMenuOpen.set(false); this.mainEdit.set(null); this.showMainForm.set(true); }
  protected openEditMain(m: MainProjectListItem): void { this.mainEdit.set(m); this.showMainForm.set(true); }
  protected openAddSub(): void { this.addMenuOpen.set(false); this.subEdit.set(null); this.subLocked.set(null); this.showSubForm.set(true); }
  protected openAddSubFor(m: MainProjectListItem): void {
    this.subEdit.set(null);
    this.subLocked.set({ id: m.id, code: m.code, name: m.name });
    this.showSubForm.set(true);
  }
  protected openEditSub(s: SubProjectListItem): void { this.subEdit.set(s); this.subLocked.set(null); this.showSubForm.set(true); }
  protected closeModals(): void { this.showMainForm.set(false); this.showSubForm.set(false); }
  protected onSaved(): void { this.closeModals(); this.load(); }

  protected deleteMain(m: MainProjectListItem): void {
    if (!confirm(`تأكيد حذف المشروع الرئيسي «${m.name}»؟`)) return;
    this.projectsService.deleteMainProject(m.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف المشروع'),
    });
  }
  protected deleteSub(s: SubProjectListItem): void {
    if (!confirm(`تأكيد حذف المشروع الفرعي «${s.name}»؟`)) return;
    this.projectsService.deleteSubProject(s.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف المشروع الفرعي'),
    });
  }

  // ===== اعتماد المشروع الفرعي =====
  protected readonly showApprovalModal = signal(false);
  protected readonly selectedSubProject = signal<SubProjectListItem | null>(null);
  protected readonly approvalCode = signal('');
  protected readonly approvalSaving = signal(false);
  protected readonly approvalError = signal<string | null>(null);

  protected openApprovalModal(subProject: SubProjectListItem, event: Event): void {
    event.stopPropagation();
    this.selectedSubProject.set(subProject);
    this.approvalCode.set('');
    this.approvalError.set(null);
    this.showApprovalModal.set(true);
  }

  protected closeApprovalModal(): void {
    this.showApprovalModal.set(false);
    this.selectedSubProject.set(null);
  }

  protected submitApproval(): void {
    if (this.approvalSaving()) return;
    this.approvalError.set(null);

    const sub = this.selectedSubProject();
    if (!sub) return;

    const code = this.approvalCode().trim();
    if (!code) {
      this.approvalError.set('برجاء إدخال كود المشروع الفرعي للاعتماد');
      return;
    }

    this.approvalSaving.set(true);
    this.projectsService.approveSubProject(sub.id, code).subscribe({
      next: (approved) => {
        this.approvalSaving.set(false);
        // حدّث الصف فورًا بدون إعادة تحميل الصفحة (isApproved مصدر الحقيقة)
        this.subs.update((list) =>
          list.map((s) =>
            s.id === approved.id
              ? {
                  ...s,
                  code: approved.code,
                  statusName: approved.statusName,
                  statusId: approved.statusId,
                  isApproved: approved.isApproved,
                  approvalCancellationReason: approved.approvalCancellationReason,
                }
              : s,
          ),
        );
        this.showApprovalModal.set(false);
        this.selectedSubProject.set(null);
      },
      error: (err) => {
        this.approvalSaving.set(false);
        this.approvalError.set(err?.error?.message ?? 'تعذّر اعتماد المشروع الفرعي');
      },
    });
  }

  // ===== إلغاء اعتماد المشروع الفرعي =====
  protected readonly showCancelApprovalModal = signal(false);
  protected readonly selectedSubProjectForCancellation = signal<SubProjectListItem | null>(null);
  protected readonly cancellationReason = signal('');
  protected readonly cancellationSaving = signal(false);
  protected readonly cancellationError = signal<string | null>(null);

  protected openCancelApprovalModal(subProject: SubProjectListItem, event: Event): void {
    event.stopPropagation();
    this.selectedSubProjectForCancellation.set(subProject);
    this.cancellationReason.set('');
    this.cancellationError.set(null);
    this.showCancelApprovalModal.set(true);
  }

  protected closeCancelApprovalModal(): void {
    this.showCancelApprovalModal.set(false);
    this.selectedSubProjectForCancellation.set(null);
  }

  protected submitCancelApproval(): void {
    if (this.cancellationSaving()) return;
    this.cancellationError.set(null);

    const sub = this.selectedSubProjectForCancellation();
    if (!sub) return;

    const reason = this.cancellationReason().trim();
    if (!reason) {
      this.cancellationError.set('برجاء إدخال سبب إلغاء الاعتماد');
      return;
    }

    this.cancellationSaving.set(true);
    this.projectsService.cancelSubProjectApproval(sub.id, reason).subscribe({
      next: (updated) => {
        this.cancellationSaving.set(false);
        this.subs.update((list) =>
          list.map((s) =>
            s.id === updated.id
              ? {
                  ...s,
                  isApproved: updated.isApproved,
                  statusName: updated.statusName,
                  statusId: updated.statusId,
                  approvalCancellationReason: updated.approvalCancellationReason,
                }
              : s,
          ),
        );
        this.showCancelApprovalModal.set(false);
        this.selectedSubProjectForCancellation.set(null);
      },
      error: (err) => {
        this.cancellationSaving.set(false);
        this.cancellationError.set(err?.error?.message ?? 'تعذّر إلغاء اعتماد المشروع الفرعي');
      },
    });
  }
}
