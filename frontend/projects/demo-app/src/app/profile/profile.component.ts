import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FileUploaderComponent, UploadEvent, FileApiService } from 'shared-lib';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FileUploaderComponent],
  template: `
    <div class="profile-page">
      <h2>Profile</h2>

      <section class="avatar-section">
        <div class="avatar-container">
          @if (avatarUrl()) {
            <img [src]="avatarUrl()" alt="Avatar" class="avatar-img" />
          } @else {
            <div class="avatar-placeholder">
              <span>{{ avatarStatus() || 'No avatar' }}</span>
            </div>
          }
        </div>

        <div class="avatar-upload">
          <h3>Upload Avatar</h3>
          <p class="hint">Select an image. It will be resized to 256×256 and 64×64 thumbnails after scanning.</p>
          <lib-file-uploader
            categoryId="image"
            ownerService="profile-service"
            label="Select Avatar Image"
            accept="image/jpeg,image/png,image/webp"
            (uploadChange)="onAvatarUpload($event)"
          />
        </div>
      </section>
    </div>
  `,
  styles: [`
    .profile-page { max-width: 600px; margin: 0 auto; padding: 24px; }
    h2 { margin-bottom: 24px; }
    .avatar-section { display: flex; gap: 24px; align-items: flex-start; margin-bottom: 24px; }
    .avatar-container { flex-shrink: 0; }
    .avatar-img { width: 128px; height: 128px; border-radius: 50%; object-fit: cover; border: 2px solid #e0e0e0; }
    .avatar-placeholder { width: 128px; height: 128px; border-radius: 50%; background: #f5f5f5; display: flex; align-items: center; justify-content: center; color: #9e9e9e; font-size: 13px; text-align: center; padding: 8px; border: 2px dashed #bdbdbd; }
    .hint { font-size: 13px; color: #757575; margin-bottom: 12px; }
  `],
})
export class ProfileComponent {
  private readonly api: FileApiService = inject(FileApiService);
  private readonly http = inject(HttpClient);

  avatarUrl = signal<string | null>(null);
  avatarStatus = signal<string | null>(null);

  onAvatarUpload(event: UploadEvent): void {
    if (event.type === 'uploaded' && event.fileId) {
      this.avatarStatus.set('Processing...');
      this.loadAvatar(event.fileId);
    }
  }

  private async loadAvatar(fileId: string): Promise<void> {
    try {
      // Simulate antivirus scan (dev only)
      await firstValueFrom(this.api.markReady(fileId));

      // Fetch image bytes via HttpClient so the auth interceptor adds Bearer token
      const blob = await firstValueFrom(
        this.http.get(`/v1/files/${fileId}/content`, { responseType: 'blob' })
      );
      this.avatarUrl.set(URL.createObjectURL(blob));
      this.avatarStatus.set(null);
    } catch {
      this.avatarStatus.set('Upload failed');
    }
  }
}
