// status.test.jsx
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { MOCK_TOKEN, MOCK_DOCUMENT } from "./mocks/handlers";
import { mockConnection } from "./setup";
import DocumentStatusPage from "../pages/DocumentStatusPage";
import DocumentStatusTracker from "../components/DocumentStatusTracker";
import { AuthProvider } from "../context/AuthContext";
vi.mock("../services/documentService", () => ({ getDocumentById: vi.fn(), getDocumentsByUser: vi.fn(), getDocumentsByApplication: vi.fn(), uploadDocument: vi.fn(), replaceDocument: vi.fn() }));
import { getDocumentById } from "../services/documentService";
const DOC_ID = MOCK_DOCUMENT.id;
function renderTracker(id = DOC_ID) { return render(<MemoryRouter><AuthProvider><DocumentStatusTracker documentId={id} intervalMs={500} /></AuthProvider></MemoryRouter>); }
function emitStatus(s, x = {}) { act(() => { mockConnection.emit("DocumentStatusUpdated", { documentId: DOC_ID, status: s, updatedAt: new Date().toISOString(), failureReason: null, ...x }); }); }
beforeEach(() => { vi.clearAllMocks(); window.localStorage.setItem("token", MOCK_TOKEN); getDocumentById.mockResolvedValue({ data: MOCK_DOCUMENT }); });

describe("Status initial load", () => {
  it("shows spinner then document details", async () => {
    renderTracker();
    expect(screen.getByText(/fetching document status/i)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(MOCK_DOCUMENT.fileName)).toBeInTheDocument());
  });
  it("displays Pending badge", async () => {
    renderTracker();
    await waitFor(() => expect(screen.getAllByText("Pending").length).toBeGreaterThan(0));
  });
  it("shows file name, type, and size", async () => {
    renderTracker();
    await waitFor(() => screen.getByText(MOCK_DOCUMENT.fileName));
    expect(screen.getByText(MOCK_DOCUMENT.documentType)).toBeInTheDocument();
    expect(screen.getByText(/142\.3 KB/)).toBeInTheDocument();
  });
  it("shows placeholder for null", () => { renderTracker(null); expect(screen.getByText(/no document id provided/i)).toBeInTheDocument(); });
  it("shows placeholder for empty", () => { renderTracker(""); expect(screen.getByText(/no document id provided/i)).toBeInTheDocument(); });
  it("shows retry on 404", async () => {
    getDocumentById.mockRejectedValue(Object.assign(new Error(), { response: { status: 404 } }));
    renderTracker("bad");
    await waitFor(() => expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument());
  });
  it("shows retry on network error", async () => {
    getDocumentById.mockRejectedValue(new Error("Network Error"));
    renderTracker();
    await waitFor(() => expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument());
  });
});

describe("Status SignalR updates", () => {
  it("updates badge on push", async () => {
    renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
    emitStatus("Processing");
    await waitFor(() => expect(screen.getAllByText("Processing").length).toBeGreaterThan(0));
  });
  it("shows Live pill", async () => {
    renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
    expect(screen.getByText(/live/i)).toBeInTheDocument();
  });
  it("final after Completed", async () => {
    renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
    emitStatus("Completed");
    await waitFor(() => expect(screen.getByText(/status is final/i)).toBeInTheDocument());
  });
  it("final after Failed", async () => {
    renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
    emitStatus("Failed");
    await waitFor(() => expect(screen.getByText(/status is final/i)).toBeInTheDocument());
  });
  it("shows failure reason", async () => {
    renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
    emitStatus("Failed", { failureReason: "HttpRequestException: Connection refused" });
    await waitFor(() => expect(screen.getByText(/HttpRequestException: Connection refused/)).toBeInTheDocument());
  });
  it("handles rapid updates", async () => {
    renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
    emitStatus("Processing"); emitStatus("Completed");
    await waitFor(() => expect(screen.getByText(/status is final/i)).toBeInTheDocument());
  });
});

describe("Status terminal states", () => {
  ["Completed", "Failed", "Verified", "UnderReview"].forEach((s) => {
    it("final after " + s, async () => {
      renderTracker(); await waitFor(() => screen.getAllByText("Pending"));
      emitStatus(s);
      await waitFor(() => expect(screen.getByText(/status is final/i)).toBeInTheDocument());
    });
  });
});

describe("Status pipeline steps", () => {
  [
    { status: "Pending", label: "Pending" }, { status: "Processing", label: "Processing" },
    { status: "Completed", label: "Completed" }, { status: "UnderReview", label: "Under Review" },
    { status: "Verified", label: "Done" },
  ].forEach(({ status, label }) => {
    it("shows " + label + " for " + status, async () => {
      getDocumentById.mockResolvedValue({ data: { ...MOCK_DOCUMENT, status } });
      renderTracker();
      await waitFor(() => screen.getByText(MOCK_DOCUMENT.fileName));
      expect(screen.getAllByText(new RegExp(label, "i")).length).toBeGreaterThan(0);
    });
  });
});

describe("Status HTTP polling", () => {
  it("calls getDocumentById on load", async () => {
    renderTracker();
    await waitFor(() => screen.getByText(MOCK_DOCUMENT.fileName));
    expect(getDocumentById).toHaveBeenCalledWith(DOC_ID);
  });
  it("refresh triggers re-fetch", async () => {
    renderTracker();
    await waitFor(() => screen.getByText(MOCK_DOCUMENT.fileName));
    const before = getDocumentById.mock.calls.length;
    await userEvent.click(screen.getByRole("button", { name: /refresh/i }));
    await waitFor(() => expect(getDocumentById.mock.calls.length).toBeGreaterThan(before));
  });
  it("retry re-fetches after error", async () => {
    getDocumentById.mockRejectedValueOnce(new Error("err")).mockResolvedValueOnce({ data: MOCK_DOCUMENT });
    renderTracker();
    await waitFor(() => screen.getByRole("button", { name: /retry/i }));
    await userEvent.click(screen.getByRole("button", { name: /retry/i }));
    await waitFor(() => screen.getByText(MOCK_DOCUMENT.fileName));
    expect(getDocumentById).toHaveBeenCalledTimes(2);
  });
});

describe("Status page routing", () => {
  it("reads id from URL", async () => {
    render(<MemoryRouter initialEntries={["/documents/doc-abc-123/status"]}><AuthProvider><Routes><Route path="/documents/:id/status" element={<DocumentStatusPage />} /></Routes></AuthProvider></MemoryRouter>);
    await waitFor(() => expect(screen.getByText(MOCK_DOCUMENT.fileName)).toBeInTheDocument());
  });
});
