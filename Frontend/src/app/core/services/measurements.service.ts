import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateMeasurement, Measurement, SetMeasurementValue, SubProjectMeasurementValue, UpdateMeasurement } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class MeasurementsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/measurements`;

  getAll(): Observable<Measurement[]> {
    return this.http.get<Measurement[]>(this.base);
  }

  getApplicable(subProgramId: number): Observable<Measurement[]> {
    return this.http.get<Measurement[]>(`${this.base}/applicable`, { params: { subProgramId } });
  }

  create(dto: CreateMeasurement): Observable<Measurement> {
    return this.http.post<Measurement>(this.base, dto);
  }

  update(id: number, dto: UpdateMeasurement): Observable<Measurement> {
    return this.http.put<Measurement>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  getValuesForSubProject(subProjectId: number): Observable<SubProjectMeasurementValue[]> {
    return this.http.get<SubProjectMeasurementValue[]>(`${environment.apiUrl}/subprojects/${subProjectId}/measurement-values`);
  }

  setValuesForSubProject(subProjectId: number, values: SetMeasurementValue[]): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/subprojects/${subProjectId}/measurement-values`, { values });
  }
}
