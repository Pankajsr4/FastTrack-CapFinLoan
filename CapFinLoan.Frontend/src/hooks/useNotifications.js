import { useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuthContext } from '../context/AuthContext';
import { getNotifications, markNotificationRead } from '../services/notificationService';

const HUB_URL = '/gateway/hubs/notifications';

export function useNotifications() {
  const { user, token } = useAuthContext();
  const [notifications, setNotifications] = useState([]);
  const [loading, setLoading]             = useState(false);
  const connectionRef                     = useRef(null);

  // ── Fetch persisted notifications from REST API ───────────────────────────
  const fetchNotifications = useCallback(async () => {
    if (!user?.sub) return;
    setLoading(true);
    try {
      const { data } = await getNotifications(user.sub);
      setNotifications(data ?? []);
    } catch (err) {
      console.error('[useNotifications] fetch error', err);
    } finally {
      setLoading(false);
    }
  }, [user?.sub]);

  // ── Mark a single notification as read ───────────────────────────────────
  const markRead = useCallback(async (id) => {
    try {
      await markNotificationRead(id);
      setNotifications(prev =>
        prev.map(n => n.id === id ? { ...n, isRead: true } : n)
      );
    } catch (err) {
      console.error('[useNotifications] markRead error', err);
    }
  }, []);

  // ── Mark all as read ──────────────────────────────────────────────────────
  const markAllRead = useCallback(async () => {
    const unread = notifications.filter(n => !n.isRead);
    await Promise.allSettled(unread.map(n => markRead(n.id)));
  }, [notifications, markRead]);

  // ── SignalR real-time connection ──────────────────────────────────────────
  useEffect(() => {
    if (!user?.sub || !token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${HUB_URL}?access_token=${token}`)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('ReceiveNotification', (notification) => {
      setNotifications(prev => [notification, ...prev]);
    });

    const start = async () => {
      try {
        await connection.start();
        // Join the user-specific group
        await connection.invoke('JoinUserGroup', user.sub);
        console.log('[useNotifications] SignalR connected, joined group:', user.sub);
      } catch (err) {
        console.warn('[useNotifications] SignalR start error', err);
      }
    };

    start();
    fetchNotifications();

    return () => {
      connection.stop();
    };
  }, [user?.sub, token, fetchNotifications]);

  const unreadCount = notifications.filter(n => !n.isRead).length;

  return { notifications, unreadCount, loading, markRead, markAllRead, refetch: fetchNotifications };
}
