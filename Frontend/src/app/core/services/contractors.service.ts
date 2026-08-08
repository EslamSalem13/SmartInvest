import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Contractor, ContractorNote, CreateContractor, UpdateContractor } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ContractorsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/contractors`;

  getAll(): Observable<Contractor[]> {
    return this.http.get<Contractor[]>(this.base);
  }

  getById(id: number): Observable<Contractor> {
    return this.http.get<Contractor>(`${this.base}/${id}`);
  }

  create(dto: CreateContractor): Observable<Contractor> {
    return this.http.post<Contractor>(this.base, dto);
  }

  update(id: number, dto: UpdateContractor): Observable<Contractor> {
    return this.http.put<Contractor>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  setWillWorkAgain(id: number, willWorkAgain: boolean | null): Observable<Contractor> {
    return this.http.put<Contractor>(`${this.base}/${id}/will-work-again`, { willWorkAgain });
  }

  addNote(id: number, text: string, subProjectId: number | null): Observable<ContractorNote> {
    return this.http.post<ContractorNote>(`${this.base}/${id}/notes`, { text, subProjectId });
  }
}
