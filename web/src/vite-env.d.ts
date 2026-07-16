/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Backend base URL (no trailing slash). Empty in dev — Vite proxy handles /api, /hubs, /uploads. */
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
