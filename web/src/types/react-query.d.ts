import '@tanstack/react-query';

declare module '@tanstack/react-query' {
  interface Register {
    queryMeta: {
      /** When true, GlobalBusyOverlay ignores this query (e.g. live polling). */
      silent?: boolean;
    };
  }
}

export {};
