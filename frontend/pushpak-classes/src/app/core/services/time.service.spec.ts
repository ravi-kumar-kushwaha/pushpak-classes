import { TestBed } from '@angular/core/testing';
import { TimeService } from './time.service';

describe('TimeService', () => {
  let service: TimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(TimeService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should calculate server offset and authoritative UTC time', () => {
    // Simulate server time 10 minutes (600,000 ms) in the future relative to client
    const clientNow = Date.now();
    const serverTimeUtc = new Date(clientNow + 600000).toISOString();

    service.syncWithServer(serverTimeUtc);

    const authoritativeNow = service.nowUtc().getTime();
    const diff = authoritativeNow - clientNow;

    // Should be approximately +600,000 ms (allowing +/- 200ms execution tolerance)
    expect(diff).toBeGreaterThanOrEqual(599800);
    expect(diff).toBeLessThanOrEqual(600200);
  });

  it('should calculate remaining seconds to EndsAtUtc', () => {
    const futureTime = new Date(Date.now() + 120000); // 120 seconds in future
    const remaining = service.getRemainingSeconds(futureTime);
    expect(remaining).toBeGreaterThanOrEqual(119);
    expect(remaining).toBeLessThanOrEqual(121);
  });

  it('should return 0 remaining seconds if EndsAtUtc is in the past', () => {
    const pastTime = new Date(Date.now() - 5000);
    const remaining = service.getRemainingSeconds(pastTime);
    expect(remaining).toBe(0);
  });

  it('should format duration into MM:SS and HH:MM:SS', () => {
    expect(service.formatDuration(0)).toBe('00:00');
    expect(service.formatDuration(65)).toBe('01:05');
    expect(service.formatDuration(3665)).toBe('01:01:05');
  });

  it('should parse ISO string without Z strictly as UTC without local timezone bias', () => {
    const withZ = service.parseUtcTimestamp('2026-11-09T08:00:00Z');
    const withoutZ = service.parseUtcTimestamp('2026-11-09T08:00:00');
    expect(withoutZ).toBe(withZ);
  });

  it('should correctly compute remaining seconds when string lacks Z', () => {
    // Current UTC time + 3600 seconds
    const futureUtc = new Date(Date.now() + 3600 * 1000);
    const isoWithoutZ = futureUtc.toISOString().replace('Z', '');
    const remaining = service.getRemainingSeconds(isoWithoutZ);
    expect(remaining).toBeGreaterThanOrEqual(3598);
    expect(remaining).toBeLessThanOrEqual(3602);
  });
});
