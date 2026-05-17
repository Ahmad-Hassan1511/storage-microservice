import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface FileCategory {
  id: string;
  name: string;
  maxSizeBytes: number;
  allowedMimeTypes: string[];
  allowedExtensions: string[];
  isLargeFile: boolean;
}

export interface InitiateUploadResponse {
  fileId: string;
  uploadUrl: string | null;
  uploadHeaders: Record<string, string>;
  expiresAt: string;
  proxyRequired: boolean;
  multipartRequired: boolean;
}

export interface CompleteUploadResponse {
  fileId: string;
  status: string;
}

export interface StoredFile {
  fileId: string;
  status: string;
  originalFileName: string;
  mimeType: string;
  sizeBytes: number;
  downloadUrl: string | null;
  previewUrl: string | null;
  thumbnailUrl: string | null;
  createdAt: string;
  expiresAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class FileApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/v1';

  getCategories(): Observable<FileCategory[]> {
    return this.http.get<FileCategory[]>(`${this.base}/categories`);
  }

  initiateUpload(body: {
    categoryId: string;
    originalFileName: string;
    mimeType: string;
    sizeBytes: number;
    idempotencyKey: string;
    ownerService: string;
  }): Observable<InitiateUploadResponse> {
    return this.http.post<InitiateUploadResponse>(`${this.base}/files`, body, {
      headers: { 'Idempotency-Key': body.idempotencyKey },
    });
  }

  completeUpload(fileId: string, checksumSha256: string, sizeBytes: number): Observable<CompleteUploadResponse> {
    return this.http.post<CompleteUploadResponse>(`${this.base}/files/${fileId}/complete`, {
      checksumSha256,
      sizeBytes,
    });
  }

  proxyUpload(fileId: string, file: File, contentType: string): Observable<void> {
    return this.http.put<void>(`${this.base}/files/${fileId}`, file, {
      headers: { 'Content-Type': contentType },
    });
  }

  getFile(fileId: string): Observable<StoredFile> {
    return this.http.get<StoredFile>(`${this.base}/files/${fileId}`);
  }

  listFiles(params?: {
    ownerService?: string;
    categoryId?: string;
    cursor?: string;
    pageSize?: number;
  }): Observable<{ items: StoredFile[]; nextCursor: string | null; totalCount: number | null }> {
    const p: Record<string, string> = {};
    if (params?.ownerService) p['ownerService'] = params.ownerService;
    if (params?.categoryId) p['categoryId'] = params.categoryId;
    if (params?.cursor) p['cursor'] = params.cursor;
    if (params?.pageSize != null) p['pageSize'] = String(params.pageSize);
    return this.http.get<{ items: StoredFile[]; nextCursor: string | null; totalCount: number | null }>(
      `${this.base}/files`, { params: p }
    );
  }

  deleteFile(fileId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/files/${fileId}`);
  }

  markReady(fileId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/dev/files/${fileId}/mark-ready`, {});
  }
}
