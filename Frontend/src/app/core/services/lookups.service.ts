import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateMarkaz,
  CreateNamedLookup,
  CreateSubProgram,
  CreateVillage,
  Lookup,
  MarkazLookup,
  SubProgramLookup,
  UpdateMarkaz,
  UpdateNamedLookup,
  UpdateSubProgram,
  UpdateVillage,
  VillageLookup,
} from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class LookupsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/lookups`;

  getPriorities(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/priorities`);
  }

  createPriority(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/priorities`, dto);
  }

  updatePriority(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/priorities/${id}`, dto);
  }

  deletePriority(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/priorities/${id}`);
  }

  getStatuses(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/statuses`);
  }

  createStatus(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/statuses`, dto);
  }

  updateStatus(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/statuses/${id}`, dto);
  }

  deleteStatus(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/statuses/${id}`);
  }

  getMainPrograms(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/main-programs`);
  }

  createMainProgram(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/main-programs`, dto);
  }

  updateMainProgram(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/main-programs/${id}`, dto);
  }

  deleteMainProgram(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/main-programs/${id}`);
  }

  getSubPrograms(mainProgramId?: number): Observable<SubProgramLookup[]> {
    let params = new HttpParams();
    if (mainProgramId != null) {
      params = params.set('mainProgramId', mainProgramId);
    }
    return this.http.get<SubProgramLookup[]>(`${this.base}/sub-programs`, { params });
  }

  createSubProgram(dto: CreateSubProgram): Observable<SubProgramLookup> {
    return this.http.post<SubProgramLookup>(`${this.base}/sub-programs`, dto);
  }

  updateSubProgram(id: number, dto: UpdateSubProgram): Observable<SubProgramLookup> {
    return this.http.put<SubProgramLookup>(`${this.base}/sub-programs/${id}`, dto);
  }

  deleteSubProgram(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/sub-programs/${id}`);
  }

  getGovernorates(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/governorates`);
  }

  createGovernorate(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/governorates`, dto);
  }

  updateGovernorate(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/governorates/${id}`, dto);
  }

  deleteGovernorate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/governorates/${id}`);
  }

  getMarkaz(governorateId?: number): Observable<MarkazLookup[]> {
    let params = new HttpParams();
    if (governorateId != null) {
      params = params.set('governorateId', governorateId);
    }
    return this.http.get<MarkazLookup[]>(`${this.base}/markaz`, { params });
  }

  createMarkaz(dto: CreateMarkaz): Observable<MarkazLookup> {
    return this.http.post<MarkazLookup>(`${this.base}/markaz`, dto);
  }

  updateMarkaz(id: number, dto: UpdateMarkaz): Observable<MarkazLookup> {
    return this.http.put<MarkazLookup>(`${this.base}/markaz/${id}`, dto);
  }

  deleteMarkaz(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/markaz/${id}`);
  }

  getVillages(markazId?: number): Observable<VillageLookup[]> {
    let params = new HttpParams();
    if (markazId != null) {
      params = params.set('markazId', markazId);
    }
    return this.http.get<VillageLookup[]>(`${this.base}/villages`, { params });
  }

  createVillage(dto: CreateVillage): Observable<VillageLookup> {
    return this.http.post<VillageLookup>(`${this.base}/villages`, dto);
  }

  updateVillage(id: number, dto: UpdateVillage): Observable<VillageLookup> {
    return this.http.put<VillageLookup>(`${this.base}/villages/${id}`, dto);
  }

  deleteVillage(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/villages/${id}`);
  }
}
