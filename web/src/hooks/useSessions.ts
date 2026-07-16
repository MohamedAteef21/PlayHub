import { useEffect, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/store';
import { SERVER_BASE } from '@/api/client';
import type { SessionLive } from '@/types';

export function useSessionHub(onUpdate: (session: SessionLive) => void, onClosed: (sessionId: string) => void) {
  const { accessToken, activeBranchId } = useAuthStore();
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    if (!accessToken || !activeBranchId) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${SERVER_BASE}/hubs/sessions?access_token=${accessToken}`)
      .withAutomaticReconnect()
      .build();

    connection.on('SessionUpdated', onUpdate);
    connection.on('SessionClosed', ({ sessionId }: { sessionId: string }) => onClosed(sessionId));

    connection
      .start()
      .then(() => connection.invoke('JoinBranch', activeBranchId))
      .then(() => setConnected(true))
      .catch(console.error);

    return () => {
      connection.stop();
      setConnected(false);
    };
  }, [accessToken, activeBranchId, onUpdate, onClosed]);

  return connected;
}

/** Server timestamps are UTC; if the 'Z' suffix was dropped, add it so the browser doesn't parse as local time. */
export function parseServerUtc(value: string): number {
  const hasZone = /Z$|[+-]\d{2}:\d{2}$/.test(value);
  return new Date(hasZone ? value : `${value}Z`).getTime();
}

export function useLiveTimer(session: SessionLive | null) {
  const [elapsed, setElapsed] = useState(0);

  const calcElapsed = useCallback(() => {
    if (!session) return 0;
    if (session.status === 2 && session.pausedAt) {
      const pausedAt = parseServerUtc(session.pausedAt);
      const started = parseServerUtc(session.startedAt);
      return Math.max(0, Math.floor((pausedAt - started) / 1000) - session.totalPausedSeconds);
    }
    const started = parseServerUtc(session.startedAt);
    return Math.max(0, Math.floor((Date.now() - started) / 1000) - session.totalPausedSeconds);
  }, [session]);

  useEffect(() => {
    setElapsed(calcElapsed());
    if (!session || session.status === 2) return;
    const id = setInterval(() => setElapsed(calcElapsed()), 1000);
    return () => clearInterval(id);
  }, [session, calcElapsed]);

  return elapsed;
}

export function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  return `${m}:${String(s).padStart(2, '0')}`;
}

export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-EG', { style: 'currency', currency: 'EGP', minimumFractionDigits: 0 }).format(amount);
}
