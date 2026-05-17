import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FileUploaderComponent, UploadEvent, FileApiService, StoredFile } from 'shared-lib';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [CommonModule, FileUploaderComponent],
  template: `
    <div class="documents-page">
      <h2>Documents</h2>

      <section class="upload-section">
        <h3>Upload Document</h3>
        <lib-file-uploader
          categoryId="document"
          ownerService="demo-app"
          label="Select Document"
          accept=".pdf,.doc,.docx,.txt"
          (uploadChange)="onUpload($event)"
        />
      </section>

      <section class="list-section">
        <div class="list-header">
          <h3>My Documents</h3>
          <button (click)="refresh()" class="refresh-btn">Refresh</button>
        </div>

        @if (loading()) {
          <p class="info">Loading...</p>
        } @else if (files().length === 0) {
          <p class="empty">No documents uploaded yet.</p>
        } @else {
          <table class="file-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
                <th>Size</th>
                <th>Uploaded</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (file of files(); track file.fileId) {
                <tr>
                  <td class="file-name">{{ file.originalFileName }}</td>
                  <td>
                    <span class="badge badge-{{ file.status }}">{{ file.status }}</span>
                  </td>
                  <td class="file-size">{{ formatSize(file.sizeBytes) }}</td>
                  <td class="file-date">{{ file.createdAt | date:'short' }}</td>
                  <td class="actions">
                    @if (file.status !== 'deleted' && file.status !== 'quarantined') {
                      <button (click)="downloadFile(file.fileId, file.originalFileName, file.status)"
                              class="btn-download">
                        Download
                      </button>
                    }
                    @if (file.status !== 'deleted') {
                      <button (click)="deleteFile(file.fileId)" class="btn-delete">Delete</button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }

        @if (nextCursor()) {
          <button (click)="loadMore()" class="load-more-btn">Load More</button>
        }

        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
      </section>
    </div>
  `,
  styles: [`
    .documents-page { max-width: 900px; margin: 0 auto; padding: 24px; }
    h2 { margin-bottom: 24px; color: #212121; }
    h3 { color: #424242; margin-bottom: 0; }
    .upload-section { background: #f5f5f5; padding: 16px; border-radius: 8px; margin-bottom: 24px; }
    .list-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
    .refresh-btn { padding: 4px 12px; background: #eeeeee; border: 1px solid #bdbdbd; border-radius: 4px; cursor: pointer; font-size: 13px; }
    .refresh-btn:hover { background: #e0e0e0; }
    .file-table { width: 100%; border-collapse: collapse; font-size: 14px; }
    .file-table th { text-align: left; padding: 8px 10px; border-bottom: 2px solid #e0e0e0; color: #616161; font-weight: 500; }
    .file-table td { padding: 8px 10px; border-bottom: 1px solid #f0f0f0; vertical-align: middle; }
    .file-name { max-width: 260px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .file-size { white-space: nowrap; color: #757575; }
    .file-date { white-space: nowrap; color: #757575; font-size: 12px; }
    .badge { padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500; text-transform: uppercase; }
    .badge-ready { background: #e8f5e9; color: #2e7d32; }
    .badge-pending, .badge-scanning { background: #fff3e0; color: #e65100; }
    .badge-quarantined { background: #fce4ec; color: #c62828; }
    .badge-deleted { background: #f5f5f5; color: #9e9e9e; }
    .actions { display: flex; gap: 8px; align-items: center; white-space: nowrap; }
    .btn-sim { padding: 3px 10px; background: #e3f2fd; color: #1565c0; border: 1px solid #90caf9; border-radius: 4px; cursor: pointer; font-size: 12px; }
    .btn-sim:hover { background: #bbdefb; }
    .btn-download { padding: 3px 10px; background: #e8f5e9; color: #2e7d32; border: 1px solid #a5d6a7; border-radius: 4px; cursor: pointer; font-size: 12px; }
    .btn-download:hover { background: #c8e6c9; }
    .btn-delete { padding: 3px 10px; background: #fce4ec; color: #c62828; border: 1px solid #ef9a9a; border-radius: 4px; cursor: pointer; font-size: 12px; }
    .btn-delete:hover { background: #ffcdd2; }
    .empty, .info { color: #9e9e9e; font-size: 14px; padding: 16px 0; }
    .error { color: #c62828; font-size: 13px; margin-top: 8px; }
    .load-more-btn { margin-top: 12px; padding: 6px 16px; background: #1976d2; color: white; border: none; border-radius: 4px; cursor: pointer; }
  `],
})
export class DocumentsComponent implements OnInit {
  private readonly api: FileApiService = inject(FileApiService);
  private readonly http = inject(HttpClient);

  files = signal<StoredFile[]>([]);
  nextCursor = signal<string | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadFiles();
  }

  onUpload(event: UploadEvent): void {
    if (event.type === 'uploaded' && event.fileId) {
      const fileId = event.fileId;
      // Simulate antivirus scan immediately after upload (dev mode)
      firstValueFrom(this.api.markReady(fileId))
        .catch(() => null)
        .then(() => this.refresh());
    }
  }

  refresh(): void {
    this.files.set([]);
    this.nextCursor.set(null);
    this.loadFiles();
  }

  private async loadFiles(cursor?: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const resp = await firstValueFrom(
        this.api.listFiles({ categoryId: 'document', cursor, pageSize: 20 })
      );
      this.files.update(prev => cursor ? [...prev, ...resp.items] : resp.items);
      this.nextCursor.set(resp.nextCursor);
    } catch {
      this.error.set('Failed to load documents.');
    } finally {
      this.loading.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const cursor = this.nextCursor();
    if (cursor) await this.loadFiles(cursor);
  }

  async downloadFile(fileId: string, fileName: string, status: string): Promise<void> {
    try {
      if (status !== 'ready') {
        await firstValueFrom(this.api.markReady(fileId));
        this.files.update(prev =>
          prev.map(f => f.fileId === fileId ? { ...f, status: 'ready' } : f)
        );
      }
      const blob = await firstValueFrom(
        this.http.get(`/v1/files/${fileId}/content`, { responseType: 'blob' })
      );
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set('Download failed.');
    }
  }

  async deleteFile(fileId: string): Promise<void> {
    try {
      await firstValueFrom(this.api.deleteFile(fileId));
      this.files.update(prev => prev.filter(f => f.fileId !== fileId));
    } catch {
      this.error.set('Failed to delete file.');
    }
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
