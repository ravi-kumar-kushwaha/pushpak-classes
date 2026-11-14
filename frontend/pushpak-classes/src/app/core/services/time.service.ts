import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class TimeService {
  /** Offset in milliseconds: serverUtc - clientLocalTime */
  private offsetMs = 0;

  /** Authoritative current UTC time signal updated every second */
  currentTimeUtc = signal<Date>(new Date());

  constructor() {
    // Tick every second to update authoritative current UTC time
    setInterval(() => {
      this.currentTimeUtc.set(this.nowUtc());
    }, 1000);
  }

  /**
   * Helper to parse any ISO string or Date into UTC epoch milliseconds.
   * If an ISO string lacks timezone info (e.g. "2026-11-09T08:09:55.123" without "Z"),
   * we append "Z" so JavaScript Date.parse() treats it strictly as UTC rather than client local time.
   */
  parseUtcTimestamp(dateVal: string | Date | null | undefined): number {
    if (!dateVal) return NaN;
    if (dateVal instanceof Date) {
      return dateVal.getTime();
    }
    if (typeof dateVal === 'string') {
      let trimmed = dateVal.trim();
      if (!trimmed) return NaN;
      // If no timezone suffix ('Z') and no explicit offset ('+05:30' or '-04:00'), enforce UTC
      if (!trimmed.endsWith('Z') && !/[+-]\d{2}(:\d{2})?$/.test(trimmed)) {
        trimmed += 'Z';
      }
      return Date.parse(trimmed);
    }
    return NaN;
  }

  /**
   * Synchronizes client offset with server UTC time.
   * Never trust client clock.
   */
  syncWithServer(serverTimeUtc: string | Date): void {
    const serverTimestamp = this.parseUtcTimestamp(serverTimeUtc);

    if (!isNaN(serverTimestamp)) {
      this.offsetMs = serverTimestamp - Date.now();
      this.currentTimeUtc.set(this.nowUtc());
    }
  }

  /**
   * Returns current server-authoritative UTC Date.
   */
  nowUtc(): Date {
    return new Date(Date.now() + this.offsetMs);
  }

  /**
   * Returns current server-authoritative ISO string in UTC.
   */
  nowUtcIso(): string {
    return this.nowUtc().toISOString();
  }

  /**
   * Calculates remaining seconds between now (authoritative) and an EndsAtUtc timestamp.
   * Returns 0 if already expired.
   */
  getRemainingSeconds(endsAtUtc: string | Date): number {
    const endTimestamp = this.parseUtcTimestamp(endsAtUtc);

    if (isNaN(endTimestamp)) return 0;

    const remainingMs = endTimestamp - this.nowUtc().getTime();
    return Math.max(0, Math.floor(remainingMs / 1000));
  }

  /**
   * Formats remaining seconds as MM:SS or HH:MM:SS
   */
  formatDuration(seconds: number): string {
    if (seconds <= 0) return '00:00';
    const hrs = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;

    const pad = (n: number) => n.toString().padStart(2, '0');

    if (hrs > 0) {
      return `${pad(hrs)}:${pad(mins)}:${pad(secs)}`;
    }
    return `${pad(mins)}:${pad(secs)}`;
  }
}
