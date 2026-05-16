import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
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
        <h3>My Documents</h3>
        @if (files().length === 0) {
          <p class="empty">No documents uploaded yet.</p>
        }
        <ul class="file-list">
          @for (file of files(); track file.fileId) {
            <li class="file-item">
              <span class="file-name">{{ file.originalFileName }}</span>
              <span class="file-status badge badge-{{ file.status }}">{{ file.status }}</span>
              @if (file.downloadUrl) {
                <a [href]="file.downloadUrl" target="_blank" class="download-link">Download</a>
              }
            </li>
          }
        </ul>

        @if (nextCursor()) {
          <button (click)="loadMore()" class="load-more-btn">Load More</button>
        }
      </section>
    </div>
  `,
  styles: [`
    .documents-page { max-width: 800px; margin: 0 auto; padding: 24px; }
    h2 { margin-bottom: 24px; color: #212121; }
    h3 { color: #424242; margin-bottom: 12px; }
    .upload-section { background: #f5f5f5; padding: 16px; border-radius: 8px; margin-bottom: 24px; }
    .file-list { list-style: none; padding: 0; margin: 0; }
    .file-item {
      display: flex; align-items: center; gap: 12px;
      padding: 10px 0; border-bottom: 1px solid #e0e0e0;
    }
    .file-name { flex: 1; font-size: 14px; }
    .badge { padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500; }
    .badge-ready { background: #e8f5e9; color: #2e7d32; }
    .badge-pending, .badge-scanning { background: #fff3e0; color: #e65100; }
    .badge-quarantined { background: #fce4ec; color: #c62828; }
    .download-link { color: #1976d2; font-size: 13px; text-decoration: none; }
    .download-link:hover { text-decoration: underline; }
    .empty { color: #9e9e9e; font-size: 14px; }
    .load-more-btn { margin-top: 12px; padding: 6px 16px; background: #1976d2; color: white; border: none; border-radius: 4px; cursor: pointer; }
  `],
})
export class DocumentsComponent implements OnInit {
  private readonly api = inject(FileApiService);

  files = signal<StoredFile[]>([]);
  nextCursor = signal<string | null>(null);

  ngOnInit(): void {
    // Initial load would come from the Documents sample API; shown empty for demo
  }

  onUpload(event: UploadEvent): void {
    if (event.type === 'uploaded' && event.fileId) {
      this.loadFile(event.fileId);
    }
  }

  private async loadFile(fileId: string): Promise<void> {
    try {
      const file = await firstValueFrom(this.api.getFile(fileId));
      this.files.update(prev => [file, ...prev]);
    } catch {
      // File may not be ready yet (still scanning); ignore
    }
  }

  async loadMore(): Promise<void> {
    // Cursor-based pagination; implementation connects to GET /v1/files with cursor param
  }
}
