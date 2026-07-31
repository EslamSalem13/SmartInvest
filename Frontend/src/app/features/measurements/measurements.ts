import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LookupsService } from '../../core/services/lookups.service';
import { MeasurementsService } from '../../core/services/measurements.service';
import { AuthService } from '../../core/services/auth.service';
import { CreateMeasurement, Lookup, Measurement, SubProgramLookup } from '../../core/models/project.models';

interface MainProgramGroup {
  id: number;
  name: string;
  subPrograms: SubProgramLookup[];
}

@Component({
  selector: 'app-measurements',
  imports: [FormsModule],
  templateUrl: './measurements.html',
  styleUrl: './measurements.css',
})
export class Measurements {
  private readonly lookups = inject(LookupsService);
  private readonly measurementsService = inject(MeasurementsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly search = signal('');

  private readonly measurements = signal<Measurement[]>([]);
  protected readonly subPrograms = signal<SubProgramLookup[]>([]);
  private readonly mainPrograms = signal<Lookup[]>([]);
  protected readonly units = signal<Lookup[]>([]);

  protected readonly mainProgramGroups = computed<MainProgramGroup[]>(() =>
    this.mainPrograms().map((mp) => ({
      id: mp.id,
      name: mp.name,
      subPrograms: this.subPrograms().filter((sp) => sp.mainProgramId === mp.id),
    })),
  );

  protected readonly expandedMainProgramIds = signal<Set<number>>(new Set());

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.measurements();
    return this.measurements().filter((m) => m.name.toLowerCase().includes(term));
  });

  protected readonly showForm = signal(false);
  protected readonly editing = signal<Measurement | null>(null);
  protected readonly fName = signal('');
  protected readonly fSubProgramIds = signal<Set<number>>(new Set());
  protected readonly fUnitIds = signal<Set<number>>(new Set());
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    Promise.all([
      new Promise<void>((resolve, reject) =>
        this.measurementsService.getAll().subscribe({ next: (v) => { this.measurements.set(v); resolve(); }, error: reject }),
      ),
      new Promise<void>((resolve, reject) =>
        this.lookups.getMainPrograms().subscribe({ next: (v) => { this.mainPrograms.set(v); resolve(); }, error: reject }),
      ),
      new Promise<void>((resolve, reject) =>
        this.lookups.getSubPrograms().subscribe({ next: (v) => { this.subPrograms.set(v); resolve(); }, error: reject }),
      ),
      new Promise<void>((resolve, reject) =>
        this.lookups.getUnits().subscribe({ next: (v) => { this.units.set(v); resolve(); }, error: reject }),
      ),
    ])
      .then(() => this.loading.set(false))
      .catch(() => {
        this.loading.set(false);
        this.error.set('تعذّر تحميل القياسات');
      });
  }

  protected toggleMainProgramExpanded(id: number): void {
    this.expandedMainProgramIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.fName.set('');
    this.fSubProgramIds.set(new Set());
    this.fUnitIds.set(new Set());
    this.expandedMainProgramIds.set(new Set());
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEdit(m: Measurement): void {
    this.editing.set(m);
    this.fName.set(m.name);
    this.fSubProgramIds.set(new Set(m.subProgramIds));
    this.fUnitIds.set(new Set(m.unitIds));
    const linkedMainProgramIds = new Set(
      this.subPrograms()
        .filter((sp) => m.subProgramIds.includes(sp.id))
        .map((sp) => sp.mainProgramId),
    );
    this.expandedMainProgramIds.set(linkedMainProgramIds);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    if (this.saving()) return;
    this.showForm.set(false);
  }

  protected toggleSubProgram(id: number): void {
    this.fSubProgramIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected toggleUnit(id: number): void {
    this.fUnitIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    const name = this.fName().trim();
    if (!name) {
      this.formError.set('برجاء إدخال اسم القياس');
      return;
    }

    const dto: CreateMeasurement = {
      name,
      subProgramIds: [...this.fSubProgramIds()],
      unitIds: [...this.fUnitIds()],
    };

    this.saving.set(true);
    const editing = this.editing();
    const req = editing
      ? this.measurementsService.update(editing.id, dto)
      : this.measurementsService.create(dto);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر الحفظ');
      },
    });
  }

  protected deleteMeasurement(m: Measurement): void {
    if (!confirm(`تأكيد حذف «${m.name}»؟`)) return;
    this.measurementsService.delete(m.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر الحذف'),
    });
  }
}
