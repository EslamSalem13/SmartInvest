import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BankAvailability, BankAvailabilityList } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class BankAvailabilitiesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/financial-years`;

  getForFinancialYear(financialYearId: number): Observable<BankAvailabilityList> {
    return this.http.get<BankAvailabilityList>(`${this.base}/${financialYearId}/bank-availabilities`);
  }

  create(financialYearId: number, formData: FormData): Observable<BankAvailability> {
    return this.http.post<BankAvailability>(`${this.base}/${financialYearId}/bank-availabilities`, formData);
  }

  /** تنزيل مستند كـ Blob عبر HttpClient (يمر بمصدّق auth.interceptor) — نفس نمط FollowUpService.downloadFile. */
  downloadDocument(financialYearId: number, availabilityId: number, documentId: number): Observable<Blob> {
    return this.http.get(`${this.base}/${financialYearId}/bank-availabilities/${availabilityId}/documents/${documentId}`, {
      responseType: 'blob',
    });
  }

  saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
