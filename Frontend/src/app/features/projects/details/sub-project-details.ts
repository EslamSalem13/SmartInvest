import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import * as L from 'leaflet';
import { ProjectsService } from '../../../core/services/projects.service';
import { MeasurementsService } from '../../../core/services/measurements.service';
import { LookupsService } from '../../../core/services/lookups.service';
import { MeasurementResolutionService } from '../../../core/services/measurement-resolution.service';
import { AuthService } from '../../../core/services/auth.service';
import { FinancialService } from '../../../core/services/financial.service';
import { formatEgpAsThousands } from '../../../core/utils/budget.util';
import {
  Lookup,
  Measurement,
  SubProjectDetail,
  SubProjectMeasurementValue,
  UpdateSubProject,
} from '../../../core/models/project.models';
import { ProcurementOverview } from '../../../core/models/financial.models';

const DEFAULT_CENTER: L.LatLngTuple = [30.5, 30.9]; // مركز محافظة المنوفية تقريبًا

type Tab = 'basic' | 'measurements' | 'location' | 'procurement';

@Component({
  selector: 'app-sub-project-details',
  imports: [FormsModule, RouterLink],
  templateUrl: './sub-project-details.html',
  styleUrl: './sub-project-details.css',
})
export class SubProjectDetails implements OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly projectsService = inject(ProjectsService);
  private readonly measurementsService = inject(MeasurementsService);
  private readonly lookups = inject(LookupsService);
  private readonly measurementResolution = inject(MeasurementResolutionService);
  private readonly financialService = inject(FinancialService);
  private readonly auth = inject(AuthService);

  protected readonly canEditProjects = this.auth.canEditProjects;
  protected readonly canManageProjects = this.auth.canManageProjects;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly project = signal<SubProjectDetail | null>(null);
  protected readonly tab = signal<Tab>('basic');

  // القياسات
  protected readonly measurementValues = signal<SubProjectMeasurementValue[]>([]);
  protected readonly measurementsLoaded = signal(false);
  protected readonly applicableMeasurements = signal<Measurement[]>([]);
  protected readonly allUnits = signal<Lookup[]>([]);
  // الطرح والعروض
  protected readonly procurementOverview = signal<ProcurementOverview | null>(null);
  protected readonly procurementLoaded = signal(false);

  // نموذج إضافة/تعديل قياس
  protected readonly showMeasurementForm = signal(false);
  protected readonly editingMeasurementId = signal<number | null>(null);
  protected readonly mName = signal('');
  protected readonly mValue = signal('');
  protected readonly mUnitName = signal('');
  protected readonly savingMeasurement = signal(false);

  // الموقع الجغرافي
  protected readonly pickedLat = signal<number | null>(null);
  protected readonly pickedLng = signal<number | null>(null);
  protected readonly savingLocation = signal(false);
  protected readonly locationSaved = signal(false);
  private map: L.Map | null = null;
  private marker: L.Marker | null = null;
  private mapInitialized = false;

  // تعثر / إلغاء تعثر
  protected readonly showMarkStalledForm = signal(false);
  protected readonly stalledReason = signal('');
  protected readonly savingStatus = signal(false);

  private subId = 0;

  protected readonly isApproved = computed(() => !!this.project()?.code);

  constructor() {
    this.subId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.projectsService.getSubProject(this.subId).subscribe({
      next: (data) => {
        this.project.set(data);
        this.pickedLat.set(data.latitude);
        this.pickedLng.set(data.longitude);
        this.locationSaved.set(false);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل تفاصيل المشروع.');
        this.loading.set(false);
      },
    });
  }

  protected setTab(t: Tab): void {
    const previous = this.tab();
    this.tab.set(t);
    if (t === 'measurements' && !this.measurementsLoaded()) {
      this.loadMeasurements();
    }
    if (t === 'procurement' && !this.procurementLoaded()) {
      this.loadProcurement();
    }
    if (previous === 'location' && t !== 'location') {
      // الـ @if يزيل حاوية الخريطة من الـ DOM عند مغادرة التاب، فيجب إعادة تهيئتها عند العودة إليه
      this.map?.remove();
      this.map = null;
      this.marker = null;
      this.mapInitialized = false;
    }
    if (t === 'location' && !this.mapInitialized) {
      this.mapInitialized = true;
      setTimeout(() => this.initMap(), 0);
    }
  }

  // ===== الموقع الجغرافي =====
  private initMap(): void {
    const container = document.getElementById('sub-project-map');
    if (!container) {
      return;
    }
    const lat = this.pickedLat();
    const lng = this.pickedLng();
    const center: L.LatLngTuple = lat != null && lng != null ? [lat, lng] : DEFAULT_CENTER;

    this.map = L.map(container).setView(center, lat != null && lng != null ? 15 : 11);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 19,
    }).addTo(this.map);

    this.marker = L.marker(center, { draggable: true }).addTo(this.map);
    this.marker.on('dragend', () => {
      const pos = this.marker!.getLatLng();
      this.pickedLat.set(pos.lat);
      this.pickedLng.set(pos.lng);
    });
    this.map.on('click', (e: L.LeafletMouseEvent) => {
      this.marker!.setLatLng(e.latlng);
      this.pickedLat.set(e.latlng.lat);
      this.pickedLng.set(e.latlng.lng);
    });
  }

  protected readonly locationChanged = computed(() => {
    const p = this.project();
    if (!p) return false;
    return p.latitude !== this.pickedLat() || p.longitude !== this.pickedLng();
  });

  protected saveLocation(): void {
    const p = this.project();
    if (!p || this.savingLocation() || !this.locationChanged()) return;

    this.savingLocation.set(true);
    const dto: UpdateSubProject = {
      code: p.code,
      name: p.name,
      projectLevelId: p.projectLevelId,
      componentTypeId: p.componentTypeId,
      accountingUnitId: p.accountingUnitId,
      projectNature: p.projectNature,
      markazId: p.markazId,
      priorityId: p.priorityId,
      statusId: p.statusId,
      bankFunding: p.bankFunding,
      selfFunding: p.selfFunding,
      latitude: this.pickedLat(),
      longitude: this.pickedLng(),
      description: p.description,
      goal: p.goal,
      socialImpact: p.socialImpact,
      economicImpact: p.economicImpact,
      environmentalImpact: p.environmentalImpact,
      greenInvestmentLink: p.greenInvestmentLink,
    };

    this.projectsService.updateSubProject(this.subId, dto).subscribe({
      next: () => {
        this.savingLocation.set(false);
        this.project.update((current) =>
          current
            ? { ...current, latitude: this.pickedLat(), longitude: this.pickedLng() }
            : current,
        );
        this.locationSaved.set(true);
      },
      error: (err) => {
        this.savingLocation.set(false);
        alert(err?.error?.message ?? 'تعذّر حفظ الموقع');
      },
    });
  }

  // ===== تعثر / إلغاء تعثر =====
  protected openMarkStalled(): void {
    this.stalledReason.set('');
    this.showMarkStalledForm.set(true);
  }

  protected closeMarkStalled(): void {
    if (this.savingStatus()) return;
    this.showMarkStalledForm.set(false);
  }

  protected confirmMarkStalled(): void {
    if (this.savingStatus()) return;
    const reason = this.stalledReason().trim();
    if (!reason) return;

    this.savingStatus.set(true);
    this.projectsService.markSubProjectStalled(this.subId, reason).subscribe({
      next: () => {
        this.savingStatus.set(false);
        this.showMarkStalledForm.set(false);
        this.load();
      },
      error: (err) => {
        this.savingStatus.set(false);
        alert(err?.error?.message ?? 'تعذّر تسجيل التعثر');
      },
    });
  }

  protected reactivate(): void {
    if (this.savingStatus()) return;
    if (!confirm('تأكيد إلغاء تعثر المشروع؟')) return;

    this.savingStatus.set(true);
    this.projectsService.reactivateSubProject(this.subId).subscribe({
      next: () => {
        this.savingStatus.set(false);
        this.load();
      },
      error: (err) => {
        this.savingStatus.set(false);
        alert(err?.error?.message ?? 'تعذّر إلغاء التعثر');
      },
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = null;
    this.marker = null;
  }

  private loadMeasurements(): void {
    const subProgramId = this.project()?.subProgramId;
    if (subProgramId == null) {
      return;
    }
    this.measurementsService.getValuesForSubProject(this.subId).subscribe({
      next: (data) => {
        this.measurementValues.set(data);
        this.measurementsLoaded.set(true);
      },
    });
    this.measurementsService.getApplicable(subProgramId).subscribe({
      next: (data) => this.applicableMeasurements.set(data),
    });
    this.lookups.getUnits().subscribe({
      next: (data) => this.allUnits.set(data),
    });
  }

  private loadProcurement(): void {
    this.financialService.getOverview(this.subId).subscribe({
      next: (data) => {
        this.procurementOverview.set(data);
        this.procurementLoaded.set(true);
      },
      error: () => {
        // لا توجد بيانات طرح مسجّلة بعد لهذا المشروع — حالة فارغة وليست خطأ
        this.procurementOverview.set(null);
        this.procurementLoaded.set(true);
      },
    });
  }

  protected thousandsLabel(value: number | null | undefined): string {
    return formatEgpAsThousands(value);
  }

  // ===== إدارة القياسات =====
  protected openAddMeasurement(): void {
    this.editingMeasurementId.set(null);
    this.mName.set('');
    this.mValue.set('');
    this.mUnitName.set('');
    this.showMeasurementForm.set(true);
  }

  protected openEditMeasurement(v: SubProjectMeasurementValue): void {
    this.editingMeasurementId.set(v.measurementId);
    this.mName.set(v.measurementName);
    this.mValue.set(v.value != null ? String(v.value) : '');
    this.mUnitName.set(v.unitName ?? '');
    this.showMeasurementForm.set(true);
  }

  protected closeMeasurementForm(): void {
    this.showMeasurementForm.set(false);
  }

  protected async saveMeasurement(): Promise<void> {
    if (this.savingMeasurement()) return;
    const name = this.mName().trim();
    const unitName = this.mUnitName().trim();
    const valueText = String(this.mValue() ?? '').trim();
    const subProgramId = this.project()?.subProgramId;
    if (!name || !unitName || !valueText || subProgramId == null) {
      return;
    }

    this.savingMeasurement.set(true);
    try {
      const resolved = await this.measurementResolution.resolveRows(
        [{ name, value: Number(valueText), unitName }],
        subProgramId,
        this.applicableMeasurements(),
        this.allUnits(),
      );
      this.applicableMeasurements.set(resolved.measurements);
      this.allUnits.set(resolved.units);

      this.measurementsService.setValuesForSubProject(this.subId, resolved.values).subscribe({
        next: () => {
          this.savingMeasurement.set(false);
          this.showMeasurementForm.set(false);
          this.measurementsLoaded.set(false);
          this.loadMeasurements();
        },
        error: (err) => {
          this.savingMeasurement.set(false);
          alert(err?.error?.message ?? 'تعذّر حفظ القياس');
        },
      });
    } catch (err: unknown) {
      this.savingMeasurement.set(false);
      const httpErr = err as { error?: { message?: string } };
      alert(httpErr?.error?.message ?? 'تعذّر معالجة القياس');
    }
  }

  protected deleteMeasurement(v: SubProjectMeasurementValue): void {
    if (!confirm(`حذف القياس «${v.measurementName}»؟`)) {
      return;
    }
    this.measurementsService
      .setValuesForSubProject(this.subId, [{ measurementId: v.measurementId, unitId: v.unitId, value: null }])
      .subscribe({
        next: () => {
          this.measurementsLoaded.set(false);
          this.loadMeasurements();
        },
        error: (err) => alert(err?.error?.message ?? 'تعذّر حذف القياس'),
      });
  }
}
