import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePresentationMemo,
  PresentationMemo,
  PresentationMemoDetail,
  ProcurementOverview,
  ProcurementStageDetail,
  ProcurementSubProjectListItem,
  ProcurementVersion,
  UpdatePresentationMemo,
} from '../models/financial.models';

@Injectable({ providedIn: 'root' })
export class FinancialService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  // ===== مراحل الطرح =====
  getSubProjects(): Observable<ProcurementSubProjectListItem[]> {
    return this.http.get<ProcurementSubProjectListItem[]>(`${this.base}/procurement/subprojects`);
  }

  getOverview(subProjectId: number): Observable<ProcurementOverview> {
    return this.http.get<ProcurementOverview>(`${this.base}/subprojects/${subProjectId}/procurement`);
  }

  getStage(subProjectId: number, stage: string): Observable<ProcurementStageDetail> {
    return this.http.get<ProcurementStageDetail>(
      `${this.base}/subprojects/${subProjectId}/procurement/${stage}`,
    );
  }

  uploadStageVersion(
    subProjectId: number,
    stage: string,
    files: Record<string, File>,
    notes: string,
  ): Observable<ProcurementVersion> {
    const form = new FormData();
    for (const [key, file] of Object.entries(files)) {
      form.append(key, file, file.name);
    }
    if (notes.trim()) {
      form.append('notes', notes.trim());
    }
    return this.http.post<ProcurementVersion>(
      `${this.base}/subprojects/${subProjectId}/procurement/${stage}/versions`,
      form,
    );
  }

  downloadStageFile(
    subProjectId: number,
    stage: string,
    versionNumber: number,
    fileKey: string,
  ): Observable<Blob> {
    return this.http.get(
      `${this.base}/subprojects/${subProjectId}/procurement/${stage}/versions/${versionNumber}/files/${fileKey}`,
      { responseType: 'blob' },
    );
  }

  completeStage(subProjectId: number, stage: string): Observable<void> {
    return this.http.put<void>(`${this.base}/subprojects/${subProjectId}/procurement/${stage}/complete`, {});
  }

  reopenStage(subProjectId: number, stage: string): Observable<void> {
    return this.http.put<void>(`${this.base}/subprojects/${subProjectId}/procurement/${stage}/reopen`, {});
  }

  /** تأكيد/إلغاء تأكيد صرف الدفعة المقدمة 25% — خاص بمرحلة العقد والترسية */
  setAdvancePaymentDone(subProjectId: number, done: boolean): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/advance-payment`,
      { done },
    );
  }

  // ===== مذكرات العرض =====
  getMemos(): Observable<PresentationMemo[]> {
    return this.http.get<PresentationMemo[]>(`${this.base}/presentation-memos`);
  }

  getMemo(id: number): Observable<PresentationMemoDetail> {
    return this.http.get<PresentationMemoDetail>(`${this.base}/presentation-memos/${id}`);
  }

  createMemo(dto: CreatePresentationMemo): Observable<PresentationMemo> {
    return this.http.post<PresentationMemo>(`${this.base}/presentation-memos`, dto);
  }

  updateMemo(id: number, dto: UpdatePresentationMemo): Observable<PresentationMemo> {
    return this.http.put<PresentationMemo>(`${this.base}/presentation-memos/${id}`, dto);
  }

  deleteMemo(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/presentation-memos/${id}`);
  }

  uploadMemoVersion(id: number, file: File, notes: string): Observable<ProcurementVersion> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (notes.trim()) {
      form.append('notes', notes.trim());
    }
    return this.http.post<ProcurementVersion>(`${this.base}/presentation-memos/${id}/versions`, form);
  }

  downloadMemoFile(id: number, versionNumber: number): Observable<Blob> {
    return this.http.get(`${this.base}/presentation-memos/${id}/versions/${versionNumber}/file`, {
      responseType: 'blob',
    });
  }

  completeMemo(id: number): Observable<void> {
    return this.http.put<void>(`${this.base}/presentation-memos/${id}/complete`, {});
  }

  reopenMemo(id: number): Observable<void> {
    return this.http.put<void>(`${this.base}/presentation-memos/${id}/reopen`, {});
  }

  /** تنزيل Blob كملف في المتصفح */
  saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
