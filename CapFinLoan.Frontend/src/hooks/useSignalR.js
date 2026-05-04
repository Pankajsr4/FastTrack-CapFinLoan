import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

const HUB_URL = import.meta.env.VITE_SIGNALR_HUB_URL;

export const CONNECTION_STATE = {
  Connecting:    'Connecting',
  Connected:     'Connected',
  Reconnecting:  'Reconnecting',
  Disconnected:  'Disconnected',
};

/**
 * Manages a single SignalR connection to DocumentStatusHub.
 *
 * - Connects on mount, disconnects on unmount.
 * - Automatic reconnect with exponential backoff (built into @microsoft/signalr).
 * - Attaches the JWT token from localStorage on every (re)connect.
 * - Exposes connectionState so the UI can show a live indicator.
 *
 * @param {string|null} documentId  — subscribes to this document's group on connect
 * @param {(update) => void} onStatusUpdate — called when "DocumentStatusUpdated" fires
 *
 * Returns:
 *   connectionState  — one of CONNECTION_STATE values
 *   subscribe(id)    — join a document group
 *   unsubscribe(id)  — leave a document group
 */
export function useSignalR(documentId, onStatusUpdate) {
  const [connectionState, setConnectionState] = useState(CONNECTION_STATE.Disconnected);
  const connectionRef = useRef(null);
  const onUpdateRef   = useRef(onStatusUpdate);

  // Keep the callback ref current without re-running the effect
  useEffect(() => { onUpdateRef.current = onStatusUpdate; }, [onStatusUpdate]);

  const subscribe = useCallback(async (id) => {
    if (!id || !connectionRef.current) return;
    try {
      await connectionRef.current.invoke('SubscribeToDocument', id);
    } catch (e) {
      console.warn('[SignalR] SubscribeToDocument failed:', e);
    }
  }, []);

  const unsubscribe = useCallback(async (id) => {
    if (!id || !connectionRef.current) return;
    try {
      await connectionRef.current.invoke('UnsubscribeFromDocument', id);
    } catch (e) {
      console.warn('[SignalR] UnsubscribeFromDocument failed:', e);
    }
  }, []);

  useEffect(() => {
    if (!HUB_URL) {
      console.warn('[SignalR] VITE_SIGNALR_HUB_URL is not set — real-time updates disabled.');
      return;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        // Attach JWT on every (re)connect attempt
        accessTokenFactory: () => localStorage.getItem('token') ?? '',
        // Prefer WebSockets, fall back to Server-Sent Events, then Long Polling
        transport: signalR.HttpTransportType.WebSockets
               | signalR.HttpTransportType.ServerSentEvents
               | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // retry delays in ms
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // ── Event listeners ───────────────────────────────────────────────────────
    connection.on('DocumentStatusUpdated', (update) => {
      onUpdateRef.current?.(update);
    });

    connection.onreconnecting(() => {
      setConnectionState(CONNECTION_STATE.Reconnecting);
    });

    connection.onreconnected(async () => {
      setConnectionState(CONNECTION_STATE.Connected);
      // Re-subscribe after reconnect — group membership is lost on disconnect
      if (documentId) await subscribe(documentId);
    });

    connection.onclose(() => {
      setConnectionState(CONNECTION_STATE.Disconnected);
    });

    // ── Start ─────────────────────────────────────────────────────────────────
    const start = async () => {
      setConnectionState(CONNECTION_STATE.Connecting);
      try {
        await connection.start();
        setConnectionState(CONNECTION_STATE.Connected);
        if (documentId) await subscribe(documentId);
      } catch (err) {
        setConnectionState(CONNECTION_STATE.Disconnected);
        console.error('[SignalR] Connection failed:', err);
      }
    };

    start();

    return () => {
      connection.stop();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // intentionally run once — documentId changes handled via subscribe()

  // Subscribe/unsubscribe when documentId changes
  useEffect(() => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      if (documentId) subscribe(documentId);
    }
    return () => {
      if (documentId) unsubscribe(documentId);
    };
  }, [documentId, subscribe, unsubscribe]);

  return { connectionState, subscribe, unsubscribe };
}
