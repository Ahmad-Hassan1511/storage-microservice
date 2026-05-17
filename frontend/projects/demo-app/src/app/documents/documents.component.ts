import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FileUploaderComponent, UploadEvent } from 'shared-lib';
import { DocumentsApiService, DocumentRecord } from '../services/documents-api.service';
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
          initiateUrl="/api/documents"
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
        } @else if (docs().length === 0) {
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
              @for (doc of docs(); track doc.id) {
                <tr>
                  <td class="file-name">{{ doc.title }}</td>
                  <td><span class="badge badge-{{ doc.status }}">{{ doc.status }}</span></td>
                  <td class="file-size">{{ formatSize(doc.sizeBytes) }}</td>
                  <td class="file-date">{{ doc.createdAt | date:'short' }}</td>
                  <td class="actions">
                    @if (doc.status === 'ready') {
                      <button (click)="downloadDoc(doc.id, doc.title)" class="btn-download">Download</button>
                    }
                    <button (click)="deleteDoc(doc.id)" class="btn-delete">Delete</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
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
    .badge-uploading, .badge-scanning { background: #fff3e0; color: #e65100; }
    .actions { display: flex; gap: 8px; align-items: center; }
    .btn-download { padding: 3px 10px; background: #e8f5e9; color: #2e7d32; border: 1px solid #a5d6a7; border-radius: 4px; cursor: pointer; font-size: 12px; }
    .btn-download:hover { background: #c8e6c9; }
    .btn-delete { padding: 3px 10px; background: #fce4ec; color: #c62828; border: 1px solid #ef9a9a; border-radius: 4px; cursor: pointer; font-size: 12px; }
    .btn-delete:hover { background: #ffcdd2; }
    .empty, .info { color: #9e9e9e; font-size: 14px; padding: 16px 0; }
    .error { color: #c62828; font-size: 13px; margin-top: 8px; }
  `],
})
export class DocumentsComponent implements OnInit {
  private readonly docsApi: DocumentsApiService = inject(DocumentsApiService);
  private readonly http = inject(HttpClient);

  docs = signal<DocumentRecord[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  ngOnInit(): void { this.loadDocs(); }

  onUpload(event: UploadEvent): void {
    if (event.type === 'uploaded') this.refresh();
  }

  refresh(): void { this.loadDocs(); }

  private async loadDocs(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.docs.set(await firstValueFrom(this.docsApi.list()));
    } catch {
      this.error.set('Failed to load documents.');
    } finally {
      this.loading.set(false);
    }
  }

  async downloadDoc(id: string, title: string): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get(`/api/documents/${id}/download`, { responseType: 'blob' })
      );
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = title; a.click();
      URL.revokeObjectURL(url);
    } catch { this.error.set('Download failed.'); }
  }

  async deleteDoc(id: string): Promise<void> {
    try {
      await firstValueFrom(this.docsApi.delete(id));
      this.docs.update(prev => prev.filter(d => d.id !== id));
    } catch { this.error.set('Failed to delete.'); }
  }

  formatSize(bytes: number | null): string {
    if (bytes == null) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
