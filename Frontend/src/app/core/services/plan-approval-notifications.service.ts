import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PagedResult,
  PlanApprovalNotificationDetail,
  PlanApprovalNotificationListItem,
  PlanApprovalNotificationStatus,
} from '../models/plan-approval-notification.models';

@Injectable({ providedIn: 'root' })
export class PlanApprovalNotificationsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/plan-approval-notifications`;

  getAll(
    page = 1,
    pageSize = 20,
    status: PlanApprovalNotificationStatus | null = null,
    financialYearId: number | null = null,
    planName = '',
    fromUtc = '',
    toUtc = '',
  ): Observable<PagedResult<PlanApprovalNotificationListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status != null) params = params.set('status', status);
    if (financialYearId != null) params = params.set('financialYearId', financialYearId);
    if (planName.trim()) params = params.set('planName', planName.trim());
    if (fromUtc) params = params.set('fromUtc', fromUtc);
    if (toUtc) params = params.set('toUtc', toUtc);
    return this.http.get<PagedResult<PlanApprovalNotificationListItem>>(this.base, { params });
  }

  getById(id: number): Observable<PlanApprovalNotificationDetail> {
    return this.http.get<PlanApprovalNotificationDetail>(`${this.base}/${id}`);
  }

  retry(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/retry`, null);
  }
}
