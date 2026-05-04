import { useState, useCallback } from 'react';
import { sendChatMessage } from '../services/aiService';

const WELCOME = '👋 Hi! I\'m your **CapFinLoan AI Assistant**. I can help with:\n• 📎 Required documents\n• 💡 Loan eligibility\n• 📊 EMI calculation\n• 📋 Application status\n\nHow can I help you today?';

export function useAiChat(applicationContext = null) {
  const [messages, setMessages] = useState([
    { role: 'ai', text: WELCOME, id: 0 }
  ]);
  const [loading, setLoading] = useState(false);

  const send = useCallback(async (text) => {
    const msg = text?.trim();
    if (!msg || loading) return;

    const userMsg = { role: 'user', text: msg, id: Date.now() };
    setMessages(prev => [...prev, userMsg]);
    setLoading(true);

    try {
      const { data } = await sendChatMessage(msg, applicationContext);
      setMessages(prev => [...prev, {
        role: 'ai',
        text: data.reply ?? 'Sorry, I couldn\'t generate a response.',
        id: Date.now() + 1
      }]);
    } catch {
      setMessages(prev => [...prev, {
        role: 'ai',
        text: '⚠️ I\'m having trouble connecting right now. Please try again in a moment.',
        id: Date.now() + 1
      }]);
    } finally {
      setLoading(false);
    }
  }, [loading, applicationContext]);

  const reset = useCallback(() => {
    setMessages([{ role: 'ai', text: WELCOME, id: 0 }]);
  }, []);

  return { messages, loading, send, reset };
}
