import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface DocumentRecord {
  id: string;
  tenantId: string;
  title: string;
  fileId: string | null;
  status: string;
  mimeType: string | null;
  sizeBytes: number | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class DocumentsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/documents';

  list(): Observable<DocumentRecord[]> {
    return this.http.get<DocumentRecord[]>(this.base);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
