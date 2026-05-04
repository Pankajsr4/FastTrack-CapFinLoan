import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';

import Navbar         from './components/Navbar';
import ProtectedRoute from './components/ProtectedRoute';

import LoginPage   from './pages/LoginPage';
import SignupPage  from './pages/SignupPage';
import NotFoundPage from './pages/NotFoundPage';
import DebugPage from './pages/DebugPage';

import DashboardPage         from './pages/DashboardPage';
import ApplicationsPage      from './pages/ApplicationsPage';
import ApplicationWizardPage from './pages/ApplicationWizardPage';
import ApplicationStatusPage from './pages/ApplicationStatusPage';
import DocumentListPage      from './pages/DocumentListPage';
import DocumentUploadPage    from './pages/DocumentUploadPage';
import DocumentStatusPage    from './pages/DocumentStatusPage';

import AdminDashboardPage         from './pages/AdminDashboardPage';
import AdminApplicationsPage      from './pages/AdminApplicationsPage';
import AdminApplicationReviewPage from './pages/AdminApplicationReviewPage';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Navbar />
        <Routes>

          {/* ── Public ──────────────────────────────────────────────────── */}
          <Route path="/login"  element={<LoginPage />} />
          <Route path="/signup" element={<SignupPage />} />
          <Route path="/debug"  element={<DebugPage />} />
          <Route path="/"       element={<Navigate to="/login" replace />} />

          {/* ── Applicant — any authenticated user ──────────────────────── */}
          <Route element={<ProtectedRoute role="APPLICANT" />}>
            <Route path="/applicant/dashboard"       element={<DashboardPage />} />
            <Route path="/applications"              element={<ApplicationsPage />} />
            <Route path="/applications/new"          element={<ApplicationWizardPage />} />
            <Route path="/applications/:id/status"   element={<ApplicationStatusPage />} />
            <Route path="/documents"                 element={<DocumentListPage />} />
            <Route path="/documents/upload"          element={<DocumentUploadPage />} />
            <Route path="/documents/:id/status"      element={<DocumentStatusPage />} />
            <Route path="/documents/status"          element={<DocumentStatusPage />} />
          </Route>

          {/* ── Admin only ──────────────────────────────────────────────── */}
          <Route element={<ProtectedRoute role="ADMIN" />}>
            <Route path="/admin/dashboard"           element={<AdminDashboardPage />} />
            <Route path="/admin/applications"        element={<AdminApplicationsPage />} />
            <Route path="/admin/applications/:id"    element={<AdminApplicationReviewPage />} />
          </Route>

          {/* ── Fallback ────────────────────────────────────────────────── */}
          <Route path="*" element={<NotFoundPage />} />

        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
