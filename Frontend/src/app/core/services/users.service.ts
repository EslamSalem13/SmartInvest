import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppUser, CreateEmployee, UpdateEmployee } from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/users`;

  getUsers(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.base);
  }

  createEmployee(dto: CreateEmployee): Observable<AppUser> {
    return this.http.post<AppUser>(this.base, dto);
  }

  updateEmployee(id: string, dto: UpdateEmployee): Observable<AppUser> {
    return this.http.put<AppUser>(`${this.base}/${id}`, dto);
  }

  resetPassword(id: string, newPassword: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/reset-password`, { newPassword });
  }

  activate(id: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/activate`, {});
  }

  deactivate(id: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}/deactivate`, {});
  }
}
