import { useState, useRef, useEffect, useMemo } from 'react';
import { Bot, X, Send, RotateCcw, Sparkles } from 'lucide-react';
import { useAiChat } from '../hooks/useAiChat';

// ── Markdown renderer: bold, bullets, line breaks ─────────────────────────────
function MarkdownText({ text }) {
  return (
    <span>
      {text.split('\n').map((line, i, arr) => {
        const parts = line.split(/\*\*(.*?)\*\*/g);
        const rendered = parts.map((p, j) =>
          j % 2 === 1 ? <strong key={j}>{p}</strong> : p
        );
        const isBullet = /^\s*[•\-\*]/.test(line);
        return (
          <span key={i}>
            {isBullet
              ? <span className="block pl-2">{rendered}</span>
              : rendered}
            {i < arr.length - 1 && <br />}
          </span>
        );
      })}
    </span>
  );
}

// ── Typing indicator ──────────────────────────────────────────────────────────
function TypingDots() {
  return (
    <div className="flex justify-start">
      <div className="bg-white border border-gray-200 rounded-2xl rounded-bl-sm px-4 py-2.5 shadow-sm">
        <span className="flex gap-1 items-center">
          {[0, 150, 300].map(delay => (
            <span key={delay}
              className="w-1.5 h-1.5 bg-gray-400 rounded-full animate-bounce"
              style={{ animationDelay: `${delay}ms` }} />
          ))}
        </span>
      </div>
    </div>
  );
}

// ── Build context-aware quick suggestions ─────────────────────────────────────
function buildSuggestions(ctx) {
  const base = [
    'What documents do I need?',
    'How much loan am I eligible for?',
    'Calculate my EMI',
    'What is my application status?',
  ];
  if (!ctx) return base;

  const extra = [];
  if (ctx.requestedAmount && ctx.tenureMonths)
    extra.push(`Calculate EMI for ₹${Number(ctx.requestedAmount).toLocaleString()} over ${ctx.tenureMonths} months`);
  if (ctx.status === 'Docs Pending')
    extra.push('What documents are still missing?');
  if (ctx.status === 'Submitted' || ctx.status === 'Under Review')
    extra.push('How long does review take?');
  if (ctx.loanPurpose)
    extra.push(`Documents needed for ${ctx.loanPurpose}`);

  return [...extra, ...base].slice(0, 4);
}

// ── Main widget ───────────────────────────────────────────────────────────────
export default function AiChatWidget({ applicationContext = null }) {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const bottomRef = useRef(null);
  const inputRef  = useRef(null);

  // Normalise context shape for the API
  const apiContext = useMemo(() => {
    if (!applicationContext) return null;
    return {
      applicationNumber:    applicationContext.applicationNumber ?? null,
      status:               applicationContext.status ?? null,
      requestedAmount:      applicationContext.requestedAmount ?? applicationContext.loanDetails?.requestedAmount ?? null,
      tenureMonths:         applicationContext.tenureMonths ?? applicationContext.loanDetails?.requestedTenureMonths ?? null,
      loanPurpose:          applicationContext.loanPurpose ?? applicationContext.loanDetails?.loanPurpose ?? null,
      monthlyIncome:        applicationContext.monthlyIncome ?? applicationContext.employmentDetails?.monthlyIncome ?? null,
      existingEmiAmount:    applicationContext.existingEmiAmount ?? applicationContext.employmentDetails?.existingEmiAmount ?? null,
      uploadedDocumentTypes: applicationContext.uploadedDocumentTypes ?? [],
    };
  }, [applicationContext]);

  const { messages, loading, send, reset } = useAiChat(apiContext);
  const suggestions = useMemo(() => buildSuggestions(apiContext), [apiContext]);

  // Scroll to bottom on new messages
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, open, loading]);

  // Focus input when opened
  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 100);
  }, [open]);

  const handleSend = () => {
    if (!input.trim() || loading) return;
    send(input.trim());
    setInput('');
  };

  const handleSuggestion = (s) => {
    send(s);
  };

  const showSuggestions = messages.length <= 1 && !loading;

  return (
    <>
      {/* ── Floating button ── */}
      <button
        onClick={() => setOpen(o => !o)}
        aria-label="Open AI Assistant"
        className={`fixed bottom-6 right-6 z-50 w-14 h-14 rounded-full shadow-lg flex items-center justify-center transition-all duration-200
          ${open
            ? 'bg-gray-700 hover:bg-gray-800 rotate-0'
            : 'bg-[#1e3a5f] hover:bg-[#0f2744] hover:scale-110'}`}
      >
        {open
          ? <X size={20} className="text-white" />
          : <Bot size={22} className="text-white" />}
        {/* Unread pulse when closed */}
        {!open && (
          <span className="absolute -top-0.5 -right-0.5 w-3.5 h-3.5 bg-green-400 rounded-full border-2 border-white" />
        )}
      </button>

      {/* ── Chat panel ── */}
      {open && (
        <div
          className="fixed bottom-24 right-6 z-50 w-80 sm:w-96 bg-white rounded-2xl shadow-2xl border border-gray-200 flex flex-col overflow-hidden fade-in"
          style={{ maxHeight: '560px' }}
        >
          {/* Header */}
          <div className="bg-[#1e3a5f] text-white px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-full bg-blue-400/30 flex items-center justify-center">
                <Sparkles size={15} className="text-blue-200" />
              </div>
              <div>
                <p className="font-semibold text-sm leading-tight">CapFinLoan AI</p>
                <p className="text-[11px] text-blue-300">Advisory only · Powered by Gemini</p>
              </div>
            </div>
            <div className="flex items-center gap-1">
              <button onClick={reset} title="Clear chat"
                className="p-1.5 rounded-lg hover:bg-white/10 transition-colors text-blue-200 hover:text-white">
                <RotateCcw size={13} />
              </button>
              <button onClick={() => setOpen(false)} title="Close"
                className="p-1.5 rounded-lg hover:bg-white/10 transition-colors text-blue-200 hover:text-white">
                <X size={14} />
              </button>
            </div>
          </div>

          {/* Context badge */}
          {apiContext?.applicationNumber && (
            <div className="px-3 py-1.5 bg-blue-50 border-b border-blue-100 flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-blue-500" />
              <span className="text-[11px] text-blue-700 font-medium">
                Context: {apiContext.applicationNumber} · {apiContext.status}
              </span>
            </div>
          )}

          {/* Messages */}
          <div className="flex-1 overflow-y-auto px-3 py-3 space-y-3 bg-gray-50" style={{ minHeight: 0 }}>
            {messages.map(m => (
              <div key={m.id} className={`flex ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                {m.role === 'ai' && (
                  <div className="w-6 h-6 rounded-full bg-[#1e3a5f] flex items-center justify-center mr-1.5 mt-0.5 flex-shrink-0">
                    <Bot size={12} className="text-white" />
                  </div>
                )}
                <div className={`max-w-[82%] px-3 py-2 rounded-2xl text-sm leading-relaxed
                  ${m.role === 'user'
                    ? 'bg-[#1e3a5f] text-white rounded-br-sm'
                    : 'bg-white text-gray-800 border border-gray-200 rounded-bl-sm shadow-sm'}`}>
                  <MarkdownText text={m.text} />
                </div>
              </div>
            ))}
            {loading && <TypingDots />}
            <div ref={bottomRef} />
          </div>

          {/* Quick suggestions */}
          {showSuggestions && (
            <div className="px-3 py-2 flex flex-wrap gap-1.5 border-t border-gray-100 bg-white">
              {suggestions.map(s => (
                <button key={s} onClick={() => handleSuggestion(s)}
                  className="text-[11px] bg-blue-50 hover:bg-blue-100 text-blue-700 px-2.5 py-1 rounded-full border border-blue-200 transition-colors leading-tight">
                  {s}
                </button>
              ))}
            </div>
          )}

          {/* Input */}
          <div className="px-3 py-2.5 border-t border-gray-200 bg-white flex gap-2 items-end">
            <textarea
              ref={inputRef}
              value={input}
              onChange={e => setInput(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault();
                  handleSend();
                }
              }}
              placeholder="Ask me anything… (Enter to send)"
              disabled={loading}
              rows={1}
              className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-400 disabled:opacity-50 resize-none"
              style={{ maxHeight: '80px', overflowY: 'auto' }}
            />
            <button
              onClick={handleSend}
              disabled={loading || !input.trim()}
              className="p-2.5 bg-[#1e3a5f] hover:bg-[#0f2744] text-white rounded-xl disabled:opacity-40 transition-colors flex-shrink-0"
              aria-label="Send"
            >
              <Send size={15} />
            </button>
          </div>

          {/* Disclaimer */}
          <p className="text-center text-[10px] text-gray-400 py-1.5 bg-white border-t border-gray-100">
            AI provides guidance only · Not a financial advisor · Does not approve/reject loans
          </p>
        </div>
      )}
    </>
  );
}
