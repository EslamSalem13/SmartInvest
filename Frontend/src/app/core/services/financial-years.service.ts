import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateFinancialYear, FinancialYear, UpdateFinancialYear } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class FinancialYearsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/financial-years`;
  private readonly selectedYearStorageKey = 'smartinvest.selectedFinancialYearId';

  resolveSelectedYearId(years: FinancialYear[], preferredId: number | null = null): number | null {
    const rememberedId = this.getRememberedYearId();
    const resolvedId = this.isAvailable(years, preferredId)
      ? preferredId
      : this.isAvailable(years, rememberedId)
        ? rememberedId
        : [...years].sort((a, b) => b.startDate.localeCompare(a.startDate))[0]?.id ?? null;

    this.rememberSelectedYearId(resolvedId);
    return resolvedId;
  }

  rememberSelectedYearId(id: number | null): void {
    try {
      if (id == null) {
        localStorage.removeItem(this.selectedYearStorageKey);
      } else {
        localStorage.setItem(this.selectedYearStorageKey, String(id));
      }
    } catch {
      // Storage can be unavailable in privacy-restricted browser contexts.
    }
  }

  private getRememberedYearId(): number | null {
    try {
      const raw = localStorage.getItem(this.selectedYearStorageKey);
      if (raw == null) return null;

      const id = Number(raw);
      return Number.isInteger(id) && id > 0 ? id : null;
    } catch {
      return null;
    }
  }

  private isAvailable(years: FinancialYear[], id: number | null): id is number {
    return id != null && years.some((year) => year.id === id);
  }

  getAll(): Observable<FinancialYear[]> {
    return this.http.get<FinancialYear[]>(this.base);
  }

  getById(id: number): Observable<FinancialYear> {
    return this.http.get<FinancialYear>(`${this.base}/${id}`);
  }

  create(dto: CreateFinancialYear): Observable<FinancialYear> {
    return this.http.post<FinancialYear>(this.base, dto);
  }

  update(id: number, dto: UpdateFinancialYear): Observable<FinancialYear> {
    return this.http.put<FinancialYear>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
