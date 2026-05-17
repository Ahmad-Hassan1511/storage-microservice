import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FileUploaderComponent, UploadEvent } from 'shared-lib';
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
          <p class="hint">Select an image file (JPEG, PNG, or WebP).</p>
          <lib-file-uploader
            initiateUrl="/api/profiles/me/avatar"
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
    .avatar-section { display: flex; gap: 24px; align-items: flex-start; }
    .avatar-img { width: 128px; height: 128px; border-radius: 50%; object-fit: cover; border: 2px solid #e0e0e0; }
    .avatar-placeholder { width: 128px; height: 128px; border-radius: 50%; background: #f5f5f5; display: flex; align-items: center; justify-content: center; color: #9e9e9e; font-size: 13px; text-align: center; padding: 8px; border: 2px dashed #bdbdbd; }
    .hint { font-size: 13px; color: #757575; margin-bottom: 12px; }
  `],
})
export class ProfileComponent implements OnInit {
  private readonly http = inject(HttpClient);

  avatarUrl = signal<string | null>(null);
  avatarStatus = signal<string | null>(null);

  ngOnInit(): void { this.loadProfile(); }

  private async loadProfile(): Promise<void> {
    try {
      const profile = await firstValueFrom(
        this.http.get<{ avatarFileId: string | null }>('/api/profiles/me')
      );
      if (profile.avatarFileId) await this.loadAvatarBlob();
    } catch { }
  }

  async onAvatarUpload(event: UploadEvent): Promise<void> {
    if (event.type === 'uploaded') {
      this.avatarStatus.set('Loading...');
      await this.loadAvatarBlob();
    }
  }

  private async loadAvatarBlob(): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get('/api/profiles/me/avatar/content', { responseType: 'blob' })
      );
      this.avatarUrl.set(URL.createObjectURL(blob));
      this.avatarStatus.set(null);
    } catch { this.avatarStatus.set('Could not load avatar'); }
  }
}
