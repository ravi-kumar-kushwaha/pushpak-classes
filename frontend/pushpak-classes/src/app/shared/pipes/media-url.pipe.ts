import { Pipe, PipeTransform } from '@angular/core';
import { environment } from '../../../environments/environment';

@Pipe({
  name: 'mediaUrl',
  standalone: true
})
export class MediaUrlPipe implements PipeTransform {
  transform(url: string | null | undefined, cacheBust?: number): string | null {
    if (!url) return null;
    let fullUrl = url;
    if (!url.startsWith('http://') && !url.startsWith('https://') && !url.startsWith('data:')) {
      const backendBase = environment.apiUrl.replace(/\/api$/, '');
      fullUrl = `${backendBase}${url.startsWith('/') ? '' : '/'}${url}`;
    }
    if (cacheBust) {
      const separator = fullUrl.includes('?') ? '&' : '?';
      fullUrl = `${fullUrl}${separator}v=${cacheBust}`;
    }
    return fullUrl;
  }
}
