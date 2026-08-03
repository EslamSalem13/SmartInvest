import { Component, computed, effect, inject, signal } from '@angular/core';
import { Perm } from '../../core/models/permission.models';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ContractorsService } from '../../core/services/contractors.service';
import { AuthService } from '../../core/services/auth.service';
import { Contractor, CreateContractor } from '../../core/models/project.models';

type StatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-contractors',
  imports: [FormsModule, RouterLink],
  templateUrl: './contractors.html',
  styleUrl: './contractors.css',
})
export class Contractors {
  private readonly contractorsService = inject(ContractorsService);
  private readonly auth = inject(AuthService);

  protected readonly canManage = computed(() => this.auth.has(Perm.ContractorsManage));

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly contractors = signal<Contractor[]>([]);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<StatusFilter>('all');
  protected readonly expandedIds = signal<Set<number>>(new Set());
  protected readonly detailLoaded = signal<Set<number>>(new Set());
  protected readonly detailError = signal<Set<number>>(new Set());

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const sf = this.statusFilter();
    return this.contractors().filter((c) => {
      const matchTerm =
        !term ||
        c.contractorName.toLowerCase().includes(term) ||
        c.category.toLowerCase().includes(term) ||
        c.phoneNumber.toLowerCase().includes(term);
      const matchStatus = sf === 'all' || (sf === 'active' ? c.isActive : !c.isActive);
      return matchTerm && matchStatus;
    });
  });

  protected readonly total = computed(() => this.contractors().length);
  protected readonly activeCount = computed(() => this.contractors().filter((c) => c.isActive).length);
  protected readonly inactiveCount = computed(() => this.contractors().filter((c) => !c.isActive).length);

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
  protected toggleExpand(c: Contractor, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.expandedIds());
    if (next.has(c.id)) {
      next.delete(c.id);
      this.expandedIds.set(next);
      return;
    }
    next.add(c.id);
    this.expandedIds.set(next);
    if (!this.detailLoaded().has(c.id)) {
      this.loadDetail(c.id);
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
    this.contractorsService.getById(id).subscribe({
      next: (full) => {
        this.contractors.update((list) => list.map((c) => (c.id === id ? full : c)));
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
  protected readonly editing = signal<Contractor | null>(null);
  protected readonly fContractorName = signal('');
  protected readonly fCompanyType = signal('');
  protected readonly fNationalId = signal('');
  protected readonly fPhone = signal('');
  protected readonly fEmail = signal('');
  protected readonly fAddress = signal('');
  protected readonly fCategory = signal('');
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
    this.contractorsService.getAll().subscribe({
      next: (data) => {
        this.contractors.set(data);
        this.loading.set(false);
        // list endpoint never populates assignedSubProjects, so any previously
        // loaded detail is now stale; clear and re-fetch for rows left expanded.
        // Also drop ids that no longer exist (e.g. just deleted) so we don't
        // re-fetch a detail for a row that will never render again.
        const existingIds = new Set(data.map((c) => c.id));
        const stillExpanded = new Set([...this.expandedIds()].filter((id) => existingIds.has(id)));
        this.expandedIds.set(stillExpanded);
        this.detailLoaded.set(new Set());
        this.detailError.set(new Set());
        for (const id of stillExpanded) {
          this.loadDetail(id);
        }
      },
      error: () => {
        this.error.set('تعذّر تحميل المقاولين. تأكد من تسجيل الدخول.');
        this.loading.set(false);
      },
    });
  }

  protected openAddForm(): void {
    this.editing.set(null);
    this.fContractorName.set('');
    this.fCompanyType.set('');
    this.fNationalId.set('');
    this.fPhone.set('');
    this.fEmail.set('');
    this.fAddress.set('');
    this.fCategory.set('');
    this.fIsActive.set(true);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEditForm(c: Contractor, event: Event): void {
    event.stopPropagation();
    this.editing.set(c);
    this.fContractorName.set(c.contractorName);
    this.fCompanyType.set(c.companyType);
    this.fNationalId.set(c.nationalIdOrCommercialRegister);
    this.fPhone.set(c.phoneNumber);
    this.fEmail.set(c.email);
    this.fAddress.set(c.address);
    this.fCategory.set(c.category);
    this.fIsActive.set(c.isActive);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    if (!this.fContractorName().trim()) {
      this.formError.set('اسم المقاول مطلوب');
      return;
    }

    const base: CreateContractor = {
      contractorName: this.fContractorName().trim(),
      companyType: this.fCompanyType().trim(),
      nationalIdOrCommercialRegister: this.fNationalId().trim(),
      phoneNumber: this.fPhone().trim(),
      email: this.fEmail().trim(),
      address: this.fAddress().trim(),
      category: this.fCategory().trim(),
    };

    this.saving.set(true);
    const editing = this.editing();
    const req = editing
      ? this.contractorsService.update(editing.id, { ...base, isActive: this.fIsActive() })
      : this.contractorsService.create(base);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ بيانات المقاول');
      },
    });
  }

  protected deleteContractor(c: Contractor, event: Event): void {
    event.stopPropagation();
    if (!confirm(`تأكيد حذف المقاول «${c.contractorName}»؟`)) return;
    this.contractorsService.delete(c.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف المقاول'),
    });
  }
}
