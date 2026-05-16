import { Component, Input, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
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
              <span>No avatar</span>
            </div>
          }
        </div>

        <div class="avatar-upload">
          <h3>Upload Avatar</h3>
          <p class="hint">Select an image. It will be resized to 256×256 and 64×64 thumbnails after scanning.</p>
          <lib-file-uploader
            categoryId="avatar"
            ownerService="profile-service"
            label="Select Avatar Image"
            accept="image/jpeg,image/png,image/webp"
            (uploadChange)="onAvatarUpload($event)"
          />
        </div>
      </section>

      @if (thumbUrl256()) {
        <section class="thumbnails-section">
          <h3>Thumbnails</h3>
          <div class="thumbs">
            <div>
              <p>256×256</p>
              <img [src]="thumbUrl256()" alt="256px thumbnail" width="256" height="256" />
            </div>
            <div>
              <p>64×64</p>
              <img [src]="thumbUrl64()" alt="64px thumbnail" width="64" height="64" />
            </div>
          </div>
        </section>
      }
    </div>
  `,
  styles: [`
    .profile-page { max-width: 600px; margin: 0 auto; padding: 24px; }
    h2 { margin-bottom: 24px; }
    .avatar-section { display: flex; gap: 24px; align-items: flex-start; margin-bottom: 24px; }
    .avatar-container { flex-shrink: 0; }
    .avatar-img { width: 128px; height: 128px; border-radius: 50%; object-fit: cover; border: 2px solid #e0e0e0; }
    .avatar-placeholder { width: 128px; height: 128px; border-radius: 50%; background: #f5f5f5; display: flex; align-items: center; justify-content: center; color: #9e9e9e; font-size: 13px; border: 2px dashed #bdbdbd; }
    .hint { font-size: 13px; color: #757575; margin-bottom: 12px; }
    .thumbs { display: flex; gap: 24px; align-items: flex-start; }
    .thumbs div { display: flex; flex-direction: column; align-items: center; gap: 4px; }
    .thumbs p { font-size: 12px; color: #616161; margin: 0; }
  `],
})
export class ProfileComponent {
  private readonly api = inject(FileApiService);

  avatarUrl = signal<string | null>(null);
  thumbUrl256 = signal<string | null>(null);
  thumbUrl64 = signal<string | null>(null);

  onAvatarUpload(event: UploadEvent): void {
    if (event.type === 'uploaded' && event.fileId) {
      this.pollForReadyState(event.fileId);
    }
  }

  private async pollForReadyState(fileId: string, attempts = 0): Promise<void> {
    if (attempts > 20) return; // give up after ~20 sec
    try {
      const file = await firstValueFrom(this.api.getFile(fileId));
      if (file.status === 'ready') {
        this.avatarUrl.set(file.downloadUrl);
        // Thumbnails would be fetched from profile-service API in a real app
      } else {
        setTimeout(() => this.pollForReadyState(fileId, attempts + 1), 1000);
      }
    } catch {
      setTimeout(() => this.pollForReadyState(fileId, attempts + 1), 1000);
    }
  }
}
