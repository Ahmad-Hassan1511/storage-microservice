import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FileApiService } from '../../services/file-api.service';
import { firstValueFrom } from 'rxjs';

export interface UploadEvent {
  type: 'started' | 'progress' | 'uploaded' | 'failed';
  fileId?: string;
  progress?: number;
  error?: string;
}

@Component({
  selector: 'lib-file-uploader',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="file-uploader">
      <label class="upload-label">
        <input
          type="file"
          [accept]="accept"
          (change)="onFileSelected($event)"
          [disabled]="uploading()"
          style="display:none"
          #fileInput
        />
        <button
          type="button"
          class="upload-btn"
          [disabled]="uploading()"
          (click)="fileInput.click()"
        >
          {{ uploading() ? 'Uploading...' : label }}
        </button>
      </label>

      @if (uploading()) {
        <div class="progress-bar">
          <div class="progress-fill" [style.width.%]="progress()"></div>
        </div>
        <span class="progress-text">{{ progress() }}%</span>
      }

      @if (errorMessage()) {
        <p class="error">{{ errorMessage() }}</p>
      }

      @if (lastFileId()) {
        <p class="success">Uploaded: {{ lastFileId() }}</p>
      }
    </div>
  `,
  styles: [`
    .file-uploader { display: flex; flex-direction: column; gap: 8px; }
    .upload-btn { padding: 8px 16px; cursor: pointer; background: #1976d2; color: white; border: none; border-radius: 4px; }
    .upload-btn:disabled { background: #ccc; cursor: not-allowed; }
    .progress-bar { height: 4px; background: #e0e0e0; border-radius: 2px; }
    .progress-fill { height: 100%; background: #1976d2; border-radius: 2px; transition: width 0.2s; }
    .progress-text { font-size: 12px; color: #666; }
    .error { color: #d32f2f; font-size: 13px; }
    .success { color: #388e3c; font-size: 13px; }
  `],
})
export class FileUploaderComponent {
  @Input() categoryId = 'document';
  @Input() ownerService = 'demo-app';
  @Input() label = 'Choose File';
  @Input() accept = '*/*';

  @Output() uploadChange = new EventEmitter<UploadEvent>();

  private readonly api = inject(FileApiService);

  uploading = signal(false);
  progress = signal(0);
  errorMessage = signal<string | null>(null);
  lastFileId = signal<string | null>(null);

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.errorMessage.set(null);
    this.lastFileId.set(null);
    this.progress.set(0);

    this.uploadChange.emit({ type: 'started' });

    try {
      const idempotencyKey = crypto.randomUUID().replace(/-/g, '');

      const initResp = await firstValueFrom(
        this.api.initiateUpload({
          categoryId: this.categoryId,
          originalFileName: file.name,
          mimeType: file.type || 'application/octet-stream',
          sizeBytes: file.size,
          idempotencyKey,
          ownerService: this.ownerService,
        })
      );

      this.progress.set(25);
      this.uploadChange.emit({ type: 'progress', progress: 25 });

      if (!initResp.proxyRequired && initResp.uploadUrl) {
        // PUT directly to presigned URL
        const headers: HeadersInit = { 'Content-Type': file.type };
        for (const [k, v] of Object.entries(initResp.uploadHeaders ?? {}))
          headers[k] = v;

        await fetch(initResp.uploadUrl, { method: 'PUT', headers, body: file });
      }

      this.progress.set(75);
      this.uploadChange.emit({ type: 'progress', progress: 75 });

      // Compute SHA-256
      const buffer = await file.arrayBuffer();
      const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
      const checksumHex = Array.from(new Uint8Array(hashBuffer))
        .map(b => b.toString(16).padStart(2, '0'))
        .join('');

      await firstValueFrom(
        this.api.completeUpload(initResp.fileId, checksumHex, file.size)
      );

      this.progress.set(100);
      this.lastFileId.set(initResp.fileId);
      this.uploadChange.emit({ type: 'uploaded', fileId: initResp.fileId, progress: 100 });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Upload failed';
      this.errorMessage.set(msg);
      this.uploadChange.emit({ type: 'failed', error: msg });
    } finally {
      this.uploading.set(false);
      input.value = '';
    }
  }
}
