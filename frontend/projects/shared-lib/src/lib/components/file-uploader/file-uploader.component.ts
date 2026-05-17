import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface UploadInitResponse {
  id: string;
  fileId: string;
  uploadUrl: string | null;
  uploadHeaders: Record<string, string>;
  proxyRequired: boolean;
  proxyUploadUrl: string | null;
  completeUrl: string;
  expiresAt: string;
}

export interface UploadEvent {
  type: 'started' | 'progress' | 'uploaded' | 'failed';
  id?: string;      // domain entity id returned by the domain service
  fileId?: string;  // storage file id
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

      @if (lastUploadedName()) {
        <p class="success">Uploaded: {{ lastUploadedName() }}</p>
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
  /** Domain service endpoint to POST to for initiating upload, e.g. '/api/documents'. */
  @Input() initiateUrl = '';
  @Input() label = 'Choose File';
  @Input() accept = '*/*';

  @Output() uploadChange = new EventEmitter<UploadEvent>();

  private readonly http = inject(HttpClient);

  uploading = signal(false);
  progress = signal(0);
  errorMessage = signal<string | null>(null);
  lastUploadedName = signal<string | null>(null);

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploading.set(true);
    this.errorMessage.set(null);
    this.lastUploadedName.set(null);
    this.progress.set(0);
    this.uploadChange.emit({ type: 'started' });

    try {
      // 1. Initiate upload with domain service
      const init = await firstValueFrom(
        this.http.post<UploadInitResponse>(this.initiateUrl, {
          fileName: file.name,
          mimeType: file.type || 'application/octet-stream',
          sizeBytes: file.size,
        })
      );

      this.progress.set(25);
      this.uploadChange.emit({ type: 'progress', progress: 25 });

      // 2. Upload bytes — either to presigned URL (object store) or domain service proxy
      if (!init.proxyRequired && init.uploadUrl) {
        // Direct to object store via presigned URL — no auth header needed
        const headers: HeadersInit = { 'Content-Type': file.type || 'application/octet-stream' };
        for (const [k, v] of Object.entries(init.uploadHeaders ?? {}))
          headers[k] = v;
        const putResp = await fetch(init.uploadUrl, { method: 'PUT', headers, body: file });
        if (!putResp.ok) throw new Error(`Upload failed: ${putResp.status}`);
      } else if (init.proxyUploadUrl) {
        // Proxy through domain service
        await firstValueFrom(
          this.http.put(init.proxyUploadUrl, file, {
            headers: { 'Content-Type': file.type || 'application/octet-stream' },
          })
        );
      }

      this.progress.set(75);
      this.uploadChange.emit({ type: 'progress', progress: 75 });

      // 3. Compute SHA-256 checksum
      const buffer = await file.arrayBuffer();
      const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
      const checksumHex = Array.from(new Uint8Array(hashBuffer))
        .map(b => b.toString(16).padStart(2, '0'))
        .join('');

      // 4. Complete upload via domain service
      await firstValueFrom(
        this.http.post(init.completeUrl, { checksumSha256: checksumHex, sizeBytes: file.size })
      );

      this.progress.set(100);
      this.lastUploadedName.set(file.name);
      this.uploadChange.emit({ type: 'uploaded', id: init.id, fileId: init.fileId, progress: 100 });
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
