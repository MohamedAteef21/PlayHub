import type { AssetDashboardDevice, DeviceReservation, SessionLive } from '@/types';
import { SessionStatus } from '@/types';

export type FloorStatusFilter = 'all' | 'attention' | 'idle' | 'active' | 'reserved' | 'paused';
export type FloorRoomFilter = 'all' | 'unassigned' | string;

export type DeviceBucket =
  | 'inactive'
  | 'timeup'
  | 'ending'
  | 'paused'
  | 'active'
  | 'reserved'
  | 'idle';

export function deviceBucket(
  device: AssetDashboardDevice,
  session: SessionLive | undefined,
  reservation: DeviceReservation | undefined
): DeviceBucket {
  if (!device.isActive) return 'inactive';
  if (session && session.status !== SessionStatus.Closed) {
    if (session.status === SessionStatus.Paused) return 'paused';
    if (session.plannedDurationMinutes != null) {
      const remaining =
        session.remainingSeconds ??
        Math.max(0, session.plannedDurationMinutes * 60 - (session.elapsedSeconds ?? 0));
      if (session.timeExpired || remaining <= 0) return 'timeup';
      if (remaining <= 300) return 'ending';
    }
    return 'active';
  }
  if (reservation) return 'reserved';
  return 'idle';
}

export function bucketPriority(bucket: DeviceBucket): number {
  switch (bucket) {
    case 'timeup':
      return 0;
    case 'ending':
      return 1;
    case 'paused':
      return 2;
    case 'active':
      return 3;
    case 'reserved':
      return 4;
    case 'idle':
      return 5;
    case 'inactive':
      return 6;
    default:
      return 9;
  }
}

export function matchesStatusFilter(bucket: DeviceBucket, filter: FloorStatusFilter): boolean {
  switch (filter) {
    case 'all':
      return true;
    case 'attention':
      return bucket === 'timeup' || bucket === 'ending' || bucket === 'paused';
    case 'idle':
      return bucket === 'idle';
    case 'active':
      return bucket === 'active' || bucket === 'ending' || bucket === 'timeup' || bucket === 'paused';
    case 'reserved':
      return bucket === 'reserved';
    case 'paused':
      return bucket === 'paused';
    default:
      return true;
  }
}

export function matchesDeviceQuery(device: AssetDashboardDevice, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  return device.name.toLowerCase().includes(q);
}

export const FLOOR_ROOM_STORAGE_KEY = 'playhub.floor.roomFilter';
export const FLOOR_STATUS_STORAGE_KEY = 'playhub.floor.statusFilter';
