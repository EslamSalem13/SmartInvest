import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CreateNamedLookup, Lookup, UpdateNamedLookup } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ContractTypesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/contract-types`;

  getAll(): Observable<Lookup[]> {
    return this.http.get<{ id: number; contractName: string }[]>(this.base).pipe(
      map((items) => items.map((i) => ({ id: i.id, name: i.contractName }))),
    );
  }

  create(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http
      .post<{ id: number; contractName: string }>(this.base, { contractName: dto.name })
      .pipe(map((i) => ({ id: i.id, name: i.contractName })));
  }

  update(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http
      .put<{ id: number; contractName: string }>(`${this.base}/${id}`, { contractName: dto.name })
      .pipe(map((i) => ({ id: i.id, name: i.contractName })));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
