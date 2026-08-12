import { HttpClient, HttpErrorResponse, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AiReportRequest,
  ReportCatalogItem,
  ReportKey,
} from '../models/report.models';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/reports`;

  getCatalog(): Observable<ReportCatalogItem[]> {
    return this.http.get<ReportCatalogItem[]>(`${this.base}/catalog`);
  }

  downloadReport(
    key: ReportKey,
    financialYearId: number | null,
  ): Observable<HttpResponse<Blob>> {
    let params = new HttpParams();
    if (financialYearId != null) {
      params = params.set('financialYearId', financialYearId);
    }

    return this.http.get(`${this.base}/${encodeURIComponent(key)}/excel`, {
      params,
      responseType: 'blob',
      observe: 'response',
    });
  }

  generateAiReport(request: AiReportRequest): Observable<HttpResponse<Blob>> {
    return this.http.post(`${this.base}/ai/excel`, request, {
      responseType: 'blob',
      observe: 'response',
    });
  }

  saveResponse(response: HttpResponse<Blob>, fallbackName: string): string {
    const blob = response.body;
    if (!blob || blob.size === 0) {
      throw new Error('The generated report is empty.');
    }

    const fileName = this.resolveFileName(
      response.headers.get('content-disposition'),
      fallbackName,
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 0);
    return fileName;
  }

  async errorMessage(error: unknown, fallback: string): Promise<string> {
    if (!(error instanceof HttpErrorResponse)) {
      return fallback;
    }

    if (error.error instanceof Blob) {
      try {
        const text = await error.error.text();
        return this.messageFromPayload(text) ?? fallback;
      } catch {
        return fallback;
      }
    }

    return this.messageFromPayload(error.error) ?? fallback;
  }

  private resolveFileName(contentDisposition: string | null, fallbackName: string): string {
    const utf8Name = contentDisposition?.match(/filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i)?.[1];
    const regularName = contentDisposition?.match(/filename\s*=\s*(?:"([^"]+)"|([^;]+))/i);
    const rawName = utf8Name ?? regularName?.[1] ?? regularName?.[2] ?? fallbackName;

    let decodedName = rawName.trim().replace(/^['"]|['"]$/g, '');
    try {
      decodedName = decodeURIComponent(decodedName);
    } catch {
      // Some servers return a plain UTF-8 filename instead of RFC 5987 encoding.
    }

    const safeName = decodedName
      .replace(/[\r\n]/g, '')
      .replace(/[\\/:*?"<>|]/g, '-')
      .trim();
    const normalized = safeName || fallbackName;
    return normalized.toLowerCase().endsWith('.xlsx') ? normalized : `${normalized}.xlsx`;
  }

  private messageFromPayload(payload: unknown): string | null {
    if (typeof payload === 'string') {
      const text = payload.trim();
      if (!text) return null;

      try {
        return this.messageFromPayload(JSON.parse(text)) ?? text;
      } catch {
        return text;
      }
    }

    if (!payload || typeof payload !== 'object') {
      return null;
    }

    const problem = payload as Record<string, unknown>;
    for (const key of ['message', 'detail', 'title']) {
      const value = problem[key];
      if (typeof value === 'string' && value.trim()) {
        return value.trim();
      }
    }

    return null;
  }
}
