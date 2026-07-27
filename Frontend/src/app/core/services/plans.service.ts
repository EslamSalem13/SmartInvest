import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApprovePlan,
  CreatedPlan,
  CreatePlan,
  Plan,
  PlanDetail,
  ProjectInfo,
  PlanProjectItem,
  UpdatePlan,
} from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class PlansService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/plans`;

  getAll(planStatus?: string, planName?: string): Observable<Plan[]> {
    const params: Record<string, string> = {};
    if (planStatus) params['planStatus'] = planStatus;
    if (planName) params['planName'] = planName;
    return this.http.get<Plan[]>(this.base, { params });
  }

  getById(id: number): Observable<PlanDetail> {
    return this.http.get<PlanDetail>(`${this.base}/${id}`);
  }

  getCurrent(): Observable<PlanDetail> {
    return this.http.get<PlanDetail>(`${this.base}/Current`);
  }

  create(dto: CreatePlan): Observable<CreatedPlan> {
    return this.http.post<CreatedPlan>(this.base, dto);
  }

  update(id: number, dto: UpdatePlan): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  addNewProject(planId: number, dto: PlanProjectItem): Observable<ProjectInfo> {
    return this.http.post<ProjectInfo>(`${this.base}/${planId}/newProject`, dto);
  }

  addExistingProject(planId: number, subProjectId: number): Observable<void> {
    return this.http.post<void>(`${this.base}/${planId}/existingProject/${subProjectId}`, null);
  }

  removeProject(planId: number, subProjectId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${planId}/projects/${subProjectId}`);
  }

  approve(planId: number, dto: ApprovePlan): Observable<PlanDetail> {
    return this.http.put<PlanDetail>(`${this.base}/${planId}/approve`, dto);
  }
}
