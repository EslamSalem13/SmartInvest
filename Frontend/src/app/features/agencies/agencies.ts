import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AgenciesService } from '../../core/services/agencies.service';
import { AuthService } from '../../core/services/auth.service';
import { CreateAgency, ExecutiveAgencyProfile } from '../../core/models/project.models';

type StatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-agencies',
  imports: [FormsModule, RouterLink],
  templateUrl: './agencies.html',
  styleUrl: './agencies.css',
})
export class Agencies {
  private readonly agenciesService = inject(AgenciesService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.canManageProjects;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly agencies = signal<ExecutiveAgencyProfile[]>([]);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<StatusFilter>('all');
  protected readonly expandedIds = signal<Set<number>>(new Set());
  protected readonly detailLoaded = signal<Set<number>>(new Set());
  protected readonly detailError = signal<Set<number>>(new Set());

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const sf = this.statusFilter();
    return this.agencies().filter((a) => {
      const matchTerm = !term || a.agencyName.toLowerCase().includes(term) || a.phone.toLowerCase().includes(term);
      const matchStatus = sf === 'all' || (sf === 'active' ? a.isActive : !a.isActive);
      return matchTerm && matchStatus;
    });
  });

  protected readonly total = computed(() => this.agencies().length);
  protected readonly activeCount = computed(() => this.agencies().filter((a) => a.isActive).length);
  protected readonly inactiveCount = computed(() => this.agencies().filter((a) => !a.isActive).length);

  // ===== pagination =====
  protected readonly page = signal(1);
  protected readonly pageSize = 8;
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  protected readonly paged = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });
  protected readonly rangeStart = computed(() =>
    this.filtered().length === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize, this.filtered().length));

  protected goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.page.set(p);
    }
  }

  // ===== expand/collapse assigned sub-projects =====
  protected toggleExpand(a: ExecutiveAgencyProfile, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.expandedIds());
    if (next.has(a.id)) {
      next.delete(a.id);
      this.expandedIds.set(next);
      return;
    }
    next.add(a.id);
    this.expandedIds.set(next);
    if (!this.detailLoaded().has(a.id)) {
      this.loadDetail(a.id);
    }
  }

  protected isExpanded(id: number): boolean {
    return this.expandedIds().has(id);
  }

  protected isDetailLoaded(id: number): boolean {
    return this.detailLoaded().has(id);
  }

  protected isDetailError(id: number): boolean {
    return this.detailError().has(id);
  }

  protected retryDetail(id: number): void {
    this.loadDetail(id);
  }

  private loadDetail(id: number): void {
    this.agenciesService.getById(id).subscribe({
      next: (full) => {
        this.agencies.update((list) => list.map((a) => (a.id === id ? full : a)));
        this.detailLoaded.update((s) => {
          const n = new Set(s);
          n.add(id);
          return n;
        });
        this.detailError.update((s) => {
          const n = new Set(s);
          n.delete(id);
          return n;
        });
      },
      error: () => {
        this.detailError.update((s) => {
          const n = new Set(s);
          n.add(id);
          return n;
        });
      },
    });
  }

  // ===== add/edit form =====
  protected readonly showForm = signal(false);
  protected readonly editing = signal<ExecutiveAgencyProfile | null>(null);
  protected readonly fAgencyName = signal('');
  protected readonly fPhone = signal('');
  protected readonly fEmail = signal('');
  protected readonly fAddress = signal('');
  protected readonly fIsActive = signal(true);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
    effect(() => {
      this.search();
      this.statusFilter();
      this.page.set(1);
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.agenciesService.getAll().subscribe({
      next: (data) => {
        this.agencies.set(data);
        this.loading.set(false);
        // list endpoint never populates assignedSubProjects, so any previously
        // loaded detail is now stale; clear and re-fetch for rows left expanded.
        // Also drop ids that no longer exist (e.g. just deleted) so we don't
        // re-fetch a detail for a row that will never render again.
        const existingIds = new Set(data.map((a) => a.id));
        const stillExpanded = new Set([...this.expandedIds()].filter((id) => existingIds.has(id)));
        this.expandedIds.set(stillExpanded);
        this.detailLoaded.set(new Set());
        this.detailError.set(new Set());
        for (const id of stillExpanded) {
          this.loadDetail(id);
        }
      },
      error: () => {
        this.error.set('تعذّر تحميل الجهات التنفيذية. تأكد من تسجيل الدخول.');
        this.loading.set(false);
      },
    });
  }

  protected openAddForm(): void {
    this.editing.set(null);
    this.fAgencyName.set('');
    this.fPhone.set('');
    this.fEmail.set('');
    this.fAddress.set('');
    this.fIsActive.set(true);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEditForm(a: ExecutiveAgencyProfile, event: Event): void {
    event.stopPropagation();
    this.editing.set(a);
    this.fAgencyName.set(a.agencyName);
    this.fPhone.set(a.phone);
    this.fEmail.set(a.email);
    this.fAddress.set(a.address);
    this.fIsActive.set(a.isActive);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    if (!this.fAgencyName().trim()) {
      this.formError.set('اسم الجهة مطلوب');
      return;
    }

    const base: CreateAgency = {
      agencyName: this.fAgencyName().trim(),
      phone: this.fPhone().trim(),
      email: this.fEmail().trim(),
      address: this.fAddress().trim(),
    };

    this.saving.set(true);
    const editing = this.editing();
    const req = editing
      ? this.agenciesService.update(editing.id, { ...base, isActive: this.fIsActive() })
      : this.agenciesService.create(base);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ بيانات الجهة');
      },
    });
  }

  protected deleteAgency(a: ExecutiveAgencyProfile, event: Event): void {
    event.stopPropagation();
    if (!confirm(`تأكيد حذف الجهة «${a.agencyName}»؟`)) return;
    this.agenciesService.delete(a.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف الجهة'),
    });
  }
}
