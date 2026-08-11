import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardOverview } from '../models/dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/dashboard`;

  getOverview(financialYearId?: number | null): Observable<DashboardOverview> {
    let params = new HttpParams();
    if (financialYearId != null) {
      params = params.set('financialYearId', financialYearId);
    }
    return this.http.get<DashboardOverview>(`${this.base}/overview`, { params });
  }
}
