import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateExecutionStagePayload,
  ExecutionStage,
  FollowUpFilters,
  FollowUpListItem,
} from '../models/follow-up.models';

@Injectable({ providedIn: 'root' })
export class FollowUpService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getList(filters: FollowUpFilters): Observable<FollowUpListItem[]> {
    const params: Record<string, string | number> = {};
    if (filters.financialYearId != null) params['financialYearId'] = filters.financialYearId;
    if (filters.mainProgramId != null) params['mainProgramId'] = filters.mainProgramId;
    if (filters.subProgramId != null) params['subProgramId'] = filters.subProgramId;
    if (filters.markazId != null) params['markazId'] = filters.markazId;
    if (filters.priorityId != null) params['priorityId'] = filters.priorityId;
    if (filters.searchTerm) params['searchTerm'] = filters.searchTerm;
    return this.http.get<FollowUpListItem[]>(`${this.base}/follow-up`, { params });
  }

  getStages(subProjectId: number, financialYearId: number): Observable<ExecutionStage[]> {
    return this.http.get<ExecutionStage[]>(`${this.base}/subprojects/${subProjectId}/execution-stages`, {
      params: { financialYearId },
    });
  }

  createStage(subProjectId: number, financialYearId: number, payload: CreateExecutionStagePayload): Observable<ExecutionStage> {
    const form = new FormData();
    form.append('financialYearId', String(financialYearId));
    form.append('name', payload.name);
    form.append('deadline', payload.deadline);
    form.append('selfFundingSpent', String(payload.selfFundingSpent));
    form.append('bankFundingSpent', String(payload.bankFundingSpent));
    form.append('physicalProgressPercent', String(payload.physicalProgressPercent));
    if (payload.notes.trim()) form.append('notes', payload.notes.trim());
    if (payload.selfFundingProof) form.append('selfFundingProof', payload.selfFundingProof, payload.selfFundingProof.name);
    if (payload.bankFundingProof) form.append('bankFundingProof', payload.bankFundingProof, payload.bankFundingProof.name);
    if (payload.physicalProgressProof) form.append('physicalProgressProof', payload.physicalProgressProof, payload.physicalProgressProof.name);

    return this.http.post<ExecutionStage>(`${this.base}/subprojects/${subProjectId}/execution-stages`, form);
  }

  updateStage(subProjectId: number, stageId: number, financialYearId: number, payload: CreateExecutionStagePayload): Observable<ExecutionStage> {
    const form = this.toStageForm(financialYearId, payload);
    return this.http.put<ExecutionStage>(`${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}`, form);
  }

  markComplete(subProjectId: number, stageId: number, financialYearId: number): Observable<ExecutionStage> {
    return this.http.put<ExecutionStage>(
      `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/complete`,
      {}, { params: { financialYearId } },
    );
  }

  reopenStage(subProjectId: number, stageId: number, financialYearId: number): Observable<ExecutionStage> {
    return this.http.put<ExecutionStage>(
      `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/reopen`,
      {}, { params: { financialYearId } },
    );
  }

  setPenalty(
    subProjectId: number,
    stageId: number,
    financialYearId: number,
    penaltyAmount: number | null,
    penaltyPaid: boolean,
  ): Observable<ExecutionStage> {
    return this.http.put<ExecutionStage>(
      `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/penalty`,
      { penaltyAmount, penaltyPaid }, { params: { financialYearId } },
    );
  }

  downloadFileUrl(subProjectId: number, stageId: number, financialYearId: number, fileKey: 'self' | 'bank' | 'progress'): string {
    return `${this.base}/subprojects/${subProjectId}/execution-stages/${stageId}/files/${fileKey}?financialYearId=${financialYearId}`;
  }

  /**
   * تنزيل ملف الإثبات كـ Blob عبر HttpClient (يمر بمصدّق auth.interceptor) — رابط <a href> مباشر
   * لا يرسل رأس Authorization فيفشل بـ 401 لأن التنقل الطبيعي للمتصفح لا يمر على الـ interceptor.
   */
  downloadFile(subProjectId: number, stageId: number, financialYearId: number, fileKey: 'self' | 'bank' | 'progress'): Observable<Blob> {
    return this.http.get(this.downloadFileUrl(subProjectId, stageId, financialYearId, fileKey), { responseType: 'blob' });
  }

  private toStageForm(financialYearId: number, payload: CreateExecutionStagePayload): FormData {
    const form = new FormData();
    form.append('financialYearId', String(financialYearId));
    form.append('name', payload.name);
    form.append('deadline', payload.deadline);
    form.append('selfFundingSpent', String(payload.selfFundingSpent));
    form.append('bankFundingSpent', String(payload.bankFundingSpent));
    form.append('physicalProgressPercent', String(payload.physicalProgressPercent));
    if (payload.notes.trim()) form.append('notes', payload.notes.trim());
    if (payload.selfFundingProof) form.append('selfFundingProof', payload.selfFundingProof, payload.selfFundingProof.name);
    if (payload.bankFundingProof) form.append('bankFundingProof', payload.bankFundingProof, payload.bankFundingProof.name);
    if (payload.physicalProgressProof) form.append('physicalProgressProof', payload.physicalProgressProof, payload.physicalProgressProof.name);
    return form;
  }

  /** تنزيل Blob كملف في المتصفح — نفس نمط FinancialService.saveBlob */
  saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
