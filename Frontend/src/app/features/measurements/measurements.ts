import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MeasurementsService } from '../../core/services/measurements.service';
import { LookupsService } from '../../core/services/lookups.service';
import { AuthService } from '../../core/services/auth.service';
import { CreateMeasurement, Measurement, SubProgramLookup } from '../../core/models/project.models';

@Component({
  selector: 'app-measurements',
  imports: [FormsModule],
  templateUrl: './measurements.html',
  styleUrl: './measurements.css',
})
export class Measurements {
  private readonly measurementsService = inject(MeasurementsService);
  private readonly lookups = inject(LookupsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly measurements = signal<Measurement[]>([]);
  protected readonly subPrograms = signal<SubProgramLookup[]>([]);
  protected readonly search = signal('');

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.measurements();
    return this.measurements().filter((m) => m.name.toLowerCase().includes(term));
  });

  protected readonly showForm = signal(false);
  protected readonly editing = signal<Measurement | null>(null);
  protected readonly fName = signal('');
  protected readonly fUnit = signal('');
  protected readonly fSubProgramIds = signal<Set<number>>(new Set());
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
    this.lookups.getSubPrograms().subscribe({ next: (list) => this.subPrograms.set(list) });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.measurementsService.getAll().subscribe({
      next: (data) => {
        this.measurements.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل القياسات');
        this.loading.set(false);
      },
    });
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.fName.set('');
    this.fUnit.set('');
    this.fSubProgramIds.set(new Set());
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEdit(m: Measurement): void {
    this.editing.set(m);
    this.fName.set(m.name);
    this.fUnit.set(m.unit);
    this.fSubProgramIds.set(new Set(m.subProgramIds));
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected toggleSubProgram(id: number): void {
    this.fSubProgramIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    if (!this.fName().trim()) {
      this.formError.set('اسم القياس مطلوب');
      return;
    }
    if (!this.fUnit().trim()) {
      this.formError.set('وحدة القياس مطلوبة');
      return;
    }

    const dto: CreateMeasurement = {
      name: this.fName().trim(),
      unit: this.fUnit().trim(),
      subProgramIds: [...this.fSubProgramIds()],
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
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ القياس');
      },
    });
  }

  protected deleteMeasurement(m: Measurement): void {
    if (!confirm(`تأكيد حذف «${m.name}»؟`)) return;
    this.measurementsService.delete(m.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف القياس'),
    });
  }
}
