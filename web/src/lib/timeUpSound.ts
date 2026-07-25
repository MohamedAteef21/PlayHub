/**
 * Audible "time is up" alert for booked sessions, generated with the Web Audio API
 * (no audio asset needed). Plays three rising beep bursts over ~4 seconds.
 */
export function playTimeUpSound() {
  const AudioCtx: typeof AudioContext | undefined =
    window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtx) return;

  const ctx = new AudioCtx();
  if (ctx.state === 'suspended') void ctx.resume();

  const beep = (startSec: number, freq: number, durSec = 0.28) => {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'square';
    osc.frequency.value = freq;
    osc.connect(gain);
    gain.connect(ctx.destination);
    const t = ctx.currentTime + startSec;
    gain.gain.setValueAtTime(0.0001, t);
    gain.gain.exponentialRampToValueAtTime(0.25, t + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, t + durSec);
    osc.start(t);
    osc.stop(t + durSec + 0.05);
  };

  // Three bursts: beep-beep ... beep-beep ... beeeep
  for (let burst = 0; burst < 3; burst++) {
    const base = burst * 1.4;
    if (burst < 2) {
      beep(base, 880);
      beep(base + 0.35, 1175);
    } else {
      beep(base, 880);
      beep(base + 0.35, 1568, 0.7);
    }
  }

  window.setTimeout(() => {
    void ctx.close();
  }, 5000);
}

/**
 * Softer double-beep ~5 minutes before a fixed booking ends.
 */
export function playEndingSoonSound() {
  const AudioCtx: typeof AudioContext | undefined =
    window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtx) return;

  const ctx = new AudioCtx();
  if (ctx.state === 'suspended') void ctx.resume();

  const beep = (startSec: number, freq: number, durSec = 0.22) => {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.value = freq;
    osc.connect(gain);
    gain.connect(ctx.destination);
    const t = ctx.currentTime + startSec;
    gain.gain.setValueAtTime(0.0001, t);
    gain.gain.exponentialRampToValueAtTime(0.18, t + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, t + durSec);
    osc.start(t);
    osc.stop(t + durSec + 0.05);
  };

  beep(0, 740);
  beep(0.32, 988);
  beep(0.9, 740);
  beep(1.22, 988);

  window.setTimeout(() => {
    void ctx.close();
  }, 2500);
}
