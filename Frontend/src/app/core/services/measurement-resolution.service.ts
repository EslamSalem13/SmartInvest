import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LookupsService } from './lookups.service';
import { MeasurementsService } from './measurements.service';
import { Lookup, Measurement, SetMeasurementValue } from '../models/project.models';

export interface MeasurementRowInput {
  name: string;
  value: number | null;
  unitName: string;
}

export interface ResolveMeasurementRowsResult {
  values: SetMeasurementValue[];
  measurements: Measurement[];
  units: Lookup[];
}

/**
 * يحل صفوف القياس (اسم/قيمة/وحدة) إلى (معرّف قياس، معرّف وحدة) - ينشئ قياسًا أو وحدة جديدة إذا لم تكن موجودة،
 * ويربط وحدة جديدة بقياس قائم إذا لزم. عند تعارض اسم القياس بين صفين بوحدتين مختلفتين
 * (مثال: "عدد" بوحدة "سيارة 30 طن" و"عدد" بوحدة "سيارة 50 طن") ينشئ قياسًا مميّزًا بالاسم
 * بدلًا من تصادم معرّف القياس نفسه في نفس الاستدعاء.
 */
@Injectable({ providedIn: 'root' })
export class MeasurementResolutionService {
  private readonly lookups = inject(LookupsService);
  private readonly measurementsService = inject(MeasurementsService);

  async resolveRows(
    rows: MeasurementRowInput[],
    subProgramId: number,
    allMeasurements: Measurement[],
    allUnits: Lookup[],
  ): Promise<ResolveMeasurementRowsResult> {
    let measurements = allMeasurements;
    let units = allUnits;
    const claimed = new Set<number>();
    const values: SetMeasurementValue[] = [];

    for (const row of rows) {
      const name = row.name.trim();
      const unitName = row.unitName.trim();

      let unit = units.find((u) => u.name.trim() === unitName);
      if (!unit) {
        unit = await firstValueFrom(this.lookups.createUnit({ name: unitName }));
        units = [...units, unit];
      }

      let measurement = measurements.find(
        (m) => m.name.trim() === name && m.subProgramIds.includes(subProgramId) && !claimed.has(m.id),
      );

      if (!measurement) {
        const collides = measurements.some((m) => m.name.trim() === name && m.subProgramIds.includes(subProgramId));
        const finalName = collides ? `${name} - ${unitName}` : name;
        measurement = await firstValueFrom(
          this.measurementsService.create({ name: finalName, subProgramIds: [subProgramId], unitIds: [unit.id] }),
        );
        measurements = [...measurements, measurement];
      } else if (!measurement.unitIds.includes(unit.id)) {
        measurement = await firstValueFrom(
          this.measurementsService.update(measurement.id, {
            name: measurement.name,
            subProgramIds: measurement.subProgramIds,
            unitIds: [...measurement.unitIds, unit.id],
          }),
        );
        measurements = measurements.map((m) => (m.id === measurement!.id ? measurement! : m));
      }

      claimed.add(measurement.id);
      values.push({ measurementId: measurement.id, unitId: unit.id, value: row.value });
    }

    return { values, measurements, units };
  }
}
