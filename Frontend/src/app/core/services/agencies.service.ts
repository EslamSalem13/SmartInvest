import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateAgency, ExecutiveAgencyProfile, UpdateAgency } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class AgenciesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/agencies`;

  getAll(): Observable<ExecutiveAgencyProfile[]> {
    return this.http.get<ExecutiveAgencyProfile[]>(this.base);
  }

  getById(id: number): Observable<ExecutiveAgencyProfile> {
    return this.http.get<ExecutiveAgencyProfile>(`${this.base}/${id}`);
  }

  create(dto: CreateAgency): Observable<ExecutiveAgencyProfile> {
    return this.http.post<ExecutiveAgencyProfile>(this.base, dto);
  }

  update(id: number, dto: UpdateAgency): Observable<ExecutiveAgencyProfile> {
    return this.http.put<ExecutiveAgencyProfile>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
