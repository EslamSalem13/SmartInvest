import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ImportCommit, ImportCommitResult, ImportPreviewResult } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ImportService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/subprojects/import`;

  preview(file: File): Observable<ImportPreviewResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ImportPreviewResult>(`${this.base}/preview`, formData);
  }

  commit(dto: ImportCommit): Observable<ImportCommitResult> {
    return this.http.post<ImportCommitResult>(`${this.base}/commit`, dto);
  }
}
