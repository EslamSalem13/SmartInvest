import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppRole, PermissionGroup, RoleDetail, SaveRole } from '../models/permission.models';

@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/roles`;

  getPermissionCatalog(): Observable<PermissionGroup[]> {
    return this.http.get<PermissionGroup[]>(`${this.base}/permissions`);
  }

  getRoles(): Observable<AppRole[]> {
    return this.http.get<AppRole[]>(this.base);
  }

  getRole(id: string): Observable<RoleDetail> {
    return this.http.get<RoleDetail>(`${this.base}/${id}`);
  }

  createRole(dto: SaveRole): Observable<RoleDetail> {
    return this.http.post<RoleDetail>(this.base, dto);
  }

  updateRole(id: string, dto: SaveRole): Observable<RoleDetail> {
    return this.http.put<RoleDetail>(`${this.base}/${id}`, dto);
  }

  deleteRole(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
