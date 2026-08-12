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
  SetContractAwardDetails,
  UpdatePresentationMemo,
} from '../models/financial.models';

@Injectable({ providedIn: 'root' })
export class FinancialService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  // ===== مراحل الطرح =====
  getSubProjects(financialYearId?: number | null): Observable<ProcurementSubProjectListItem[]> {
    const params: Record<string, number> = {};
    if (financialYearId != null) {
      params['financialYearId'] = financialYearId;
    }
    return this.http.get<ProcurementSubProjectListItem[]>(`${this.base}/procurement/subprojects`, { params });
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

  /** مدير التخطيط يحدد المدة القصوى (بالأيام) — null يلغي الموعد النهائي. غير متاحة لمرحلة الإعلان. */
  setStageDuration(subProjectId: number, stage: string, durationDays: number | null): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/${stage}/duration`,
      { durationDays },
    );
  }

  /** تاريخ نشر الإعلان — خاص بمرحلة الإعلان، منه تُحسب مدة الـ15 يومًا الإلزامية */
  setAnnouncementDate(subProjectId: number, announcementDate: string): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/announcement/date`,
      { announcementDate },
    );
  }

  /** "هذه المرحلة غير لازمة للطرح" */
  skipStage(subProjectId: number, stage: string, reason: string): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/${stage}/skip`,
      { reason },
    );
  }

  /** فشل مرحلة — يبطل اكتمالها وما بعدها، ويتطلب سببًا */
  failStage(subProjectId: number, stage: string, reason: string): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/${stage}/fail`,
      { reason },
    );
  }

  /** تأكيد/إلغاء تأكيد صرف الدفعة المقدمة — خاص بمرحلة العقد والترسية */
  setAdvancePaymentDone(subProjectId: number, done: boolean): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/advance-payment`,
      { done },
    );
  }

  /** حفظ بيانات الترسية: المقاول، الدفعة المقدمة، مدة التنفيذ، الشرط الجزائي */
  setContractAwardDetails(subProjectId: number, dto: SetContractAwardDetails): Observable<void> {
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/details`,
      dto,
    );
  }

  /** تسجيل تسليم الأرضية — multipart: التاريخ + ملف الإثبات */
  setSiteHandover(subProjectId: number, handoverDate: string, proof: File): Observable<void> {
    const form = new FormData();
    form.append('handoverDate', handoverDate);
    form.append('proof', proof, proof.name);
    return this.http.put<void>(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/site-handover`,
      form,
    );
  }

  /** تنزيل إثبات تسليم الأرضية كـ Blob (رابط مباشر يفشل بـ 401 لأنه لا يمر على auth.interceptor) */
  downloadSiteHandoverProof(subProjectId: number): Observable<Blob> {
    return this.http.get(
      `${this.base}/subprojects/${subProjectId}/procurement/contract-award/site-handover/proof`,
      { responseType: 'blob' },
    );
  }

  // ===== مذكرات العرض =====
  getMemos(financialYearId?: number | null): Observable<PresentationMemo[]> {
    const params: Record<string, number> = {};
    if (financialYearId != null) {
      params['financialYearId'] = financialYearId;
    }
    return this.http.get<PresentationMemo[]>(`${this.base}/presentation-memos`, { params });
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

  uploadMemoVersion(
    id: number,
    file: File,
    notes: string,
    legalAffairsCommitteeDecision?: File | null,
  ): Observable<ProcurementVersion> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (notes.trim()) {
      form.append('notes', notes.trim());
    }
    if (legalAffairsCommitteeDecision) {
      form.append(
        'legalAffairsCommitteeDecision',
        legalAffairsCommitteeDecision,
        legalAffairsCommitteeDecision.name,
      );
    }
    return this.http.post<ProcurementVersion>(`${this.base}/presentation-memos/${id}/versions`, form);
  }

  /** إرفاق قرار لجنة الشؤون القانونية بالإصدار الحالي — لا يُنشئ إصدارًا جديدًا */
  uploadLegalDecision(id: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<void>(`${this.base}/presentation-memos/${id}/legal-decision`, form);
  }

  downloadMemoFile(id: number, versionNumber: number, fileKey?: string): Observable<Blob> {
    return this.http.get(`${this.base}/presentation-memos/${id}/versions/${versionNumber}/file`, {
      responseType: 'blob',
      params: fileKey ? { fileKey } : {},
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
