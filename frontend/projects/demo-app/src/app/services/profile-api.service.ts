import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface UserProfileRecord {
  userId: string;
  avatarStatus: string | null;
  avatarFileId: string | null;
  avatarUrl256: string | null;
  avatarUrl64: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProfileApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/profiles';

  getProfile(): Observable<UserProfileRecord> {
    return this.http.get<UserProfileRecord>(`${this.base}/me`);
  }

  updateAvatar(fileId: string): Observable<UserProfileRecord> {
    return this.http.post<UserProfileRecord>(`${this.base}/me/avatar`, { fileId });
  }
}
