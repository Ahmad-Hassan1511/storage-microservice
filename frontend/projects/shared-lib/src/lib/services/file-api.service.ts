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

  getFile(fileId: string): Observable<StoredFile> {
    return this.http.get<StoredFile>(`${this.base}/files/${fileId}`);
  }
}
