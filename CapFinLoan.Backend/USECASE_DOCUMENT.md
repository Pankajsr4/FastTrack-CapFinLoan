# CapFinLoan — Use Case Document

## System Overview

CapFinLoan is a microservices-based loan application and document management portal. It allows applicants to apply for loans, upload supporting documents, and track their application status. Admins review applications, verify documents, and make lending decisions.

---

## Actors

| Actor | Description |
|-------|-------------|
| **Applicant** | A registered user who applies for a loan and submits documents |
| **Admin** | A staff member who reviews applications and verifies documents |
| **System** | Automated background processes (RabbitMQ consumers, status transitions) |

---

## Use Cases

### UC-01: Register Account

**Actor:** Applicant / Admin  
**Precondition:** User does not have an existing account  
**Main Flow:**
1. User navigates to the Sign Up page
2. User selects role (Applicant or Admin)
3. User fills in name, email, phone, and password
4. System validates input (min 8 chars, unique email)
5. System creates account, generates JWT token
6. System redirects to role-specific dashboard

**Alternate Flow:**
- Email already exists → system shows error "An account already exists with this email"

---

### UC-02: Login

**Actor:** Applicant / Admin  
**Precondition:** User has a registered account  
**Main Flow:**
1. User enters email and password
2. System validates credentials
3. System issues JWT token (60-minute expiry)
4. System redirects: Applicant → `/applicant/dashboard`, Admin → `/admin/dashboard`

**Alternate Flow:**
- Invalid credentials → "Invalid email or password"
- Deactivated account → "User is deactivated"

---

### UC-03: Create Loan Application

**Actor:** Applicant  
**Precondition:** Applicant is logged in  
**Main Flow:**
1. Applicant clicks "New Application" on dashboard
2. System presents 4-step wizard:
   - Step 1: Personal Details (name, DOB, address, contact)
   - Step 2: Employment Details (employer, income, EMI)
   - Step 3: Loan Details (amount, tenure, purpose)
   - Step 4: Review summary
3. Each step auto-saves as a Draft
4. Applicant submits on Step 4
5. System transitions status: Draft → Submitted
6. System publishes `ApplicationSubmittedEvent` via RabbitMQ
7. Admin service receives event and adds application to review queue

**Constraints:**
- Loan amount: ₹10,000 – ₹50,00,000
- Tenure: 6 – 360 months

---

### UC-04: View My Applications

**Actor:** Applicant  
**Precondition:** Applicant is logged in  
**Main Flow:**
1. Applicant navigates to "My Loans"
2. System displays all applications with status badges
3. Applicant clicks an application to view status timeline

---

### UC-05: Upload Document

**Actor:** Applicant  
**Precondition:** Applicant has at least one submitted application  
**Main Flow:**
1. Applicant navigates to "Upload Document"
2. Applicant selects application from dropdown
3. Applicant selects document type (NationalId, ProofOfIncome, BankStatement, AddressProof, Other)
4. Applicant selects file (PDF, JPG, PNG — max 5 MB)
5. System uploads file, stores metadata, sets status to Pending
6. System publishes `DocumentUploadedEvent` via RabbitMQ
7. Admin service receives event and adds document to processing queue
8. System shows success with Document ID

**Alternate Flow:**
- File too large → "File must be 5 MB or smaller"
- Invalid file type → "Only PDF, JPG, and PNG files are allowed"

---

### UC-06: Track Document Status

**Actor:** Applicant  
**Precondition:** Applicant has uploaded at least one document  
**Main Flow:**
1. Applicant navigates to "Documents"
2. System auto-loads all uploaded documents
3. Applicant can search by file name/type and filter by status
4. Applicant clicks "Track →" on a document
5. System shows real-time status via SignalR WebSocket

**Document Status Lifecycle:**
```
Pending → Processing → Completed → UnderReview → Verified
                                               ↘ ReuploadRequired
                    ↘ Failed
```

---

### UC-07: View Application Status Timeline

**Actor:** Applicant  
**Precondition:** Application has been submitted  
**Main Flow:**
1. Applicant clicks on an application
2. System displays status progress bar (Draft → Submitted → Docs Pending → Docs Verified → Under Review → Approved/Rejected)
3. System shows full status history with timestamps and remarks

---

### UC-08: Review Application (Admin)

**Actor:** Admin  
**Precondition:** Admin is logged in; application exists in admin queue  
**Main Flow:**
1. Admin navigates to Dashboard
2. Admin searches/filters applications by name, email, or application number
3. Admin clicks "Review →" on an application
4. System displays:
   - Applicant personal details
   - Loan details (amount, tenure, purpose, income)
   - Uploaded documents with status badges
5. Admin views document files (PDF viewer)
6. Admin selects new status (Under Review / Docs Pending / Approved / Rejected / Disbursed)
7. Admin adds remarks (required for Rejected/Docs Pending)
8. Admin clicks "Save Decision"
9. System updates status in admin DB
10. System publishes `ApplicationStatusChangedEvent` via RabbitMQ
11. Application service receives event and updates applicant-facing status

**Status Transition Rules:**
| From | Allowed To |
|------|-----------|
| Submitted | UnderReview, PendingDocuments, Approved, Rejected |
| PendingDocuments | UnderReview, DocsVerified, Approved, Rejected |
| DocsVerified | UnderReview, Approved, Rejected |
| UnderReview | Approved, Rejected, PendingDocuments |
| Approved | (terminal) |
| Rejected | (terminal) |

---

### UC-09: Verify Document (Admin)

**Actor:** Admin  
**Precondition:** Admin is on the application review page; documents are in Pending or UnderReview status  
**Main Flow:**
1. Admin views document in the Documents section
2. Admin clicks "👁 View" to open the PDF
3. Admin clicks "✓ Approve" or "✗ Reject"
4. System updates document status:
   - Approve → Verified
   - Reject → ReuploadRequired
5. System publishes `DocumentVerifiedEvent` via RabbitMQ
6. Applicant sees updated status in real-time via SignalR

---

### UC-10: Search Applications (Admin)

**Actor:** Admin  
**Precondition:** Admin is on the dashboard  
**Main Flow:**
1. Admin types in the search bar
2. System filters applications in real-time by:
   - Applicant name
   - Email address
   - Application number
3. Admin uses status filter tabs (All / Submitted / Docs Pending / Under Review / Approved / Rejected)

---

### UC-11: Logout

**Actor:** Applicant / Admin  
**Main Flow:**
1. User clicks "Sign out"
2. System clears JWT token from localStorage
3. System redirects to login page

---

## Application Status Lifecycle

```
Draft
  ↓ (applicant submits)
Submitted
  ↓ (admin action)
Docs Pending ←→ Under Review
  ↓ (all docs verified)
Docs Verified
  ↓ (admin decision)
Approved  |  Rejected
```

---

## Event Flow (RabbitMQ)

| Event | Publisher | Consumer | Effect |
|-------|-----------|----------|--------|
| `ApplicationSubmittedEvent` | Application Service | Admin Service | Creates application in admin review queue |
| `DocumentUploadedEvent` | Document Service | Admin Service | Creates document processing record |
| `DocumentVerifiedEvent` | Document Service | Admin Service | Updates document processing record |
| `ApplicationStatusChangedEvent` | Admin Service | Application Service | Updates applicant-facing application status |
| `UserRegisteredEvent` | Auth Service | (logged) | Audit trail |

---

## Non-Functional Requirements

| Requirement | Implementation |
|-------------|---------------|
| Authentication | JWT Bearer tokens, 60-min expiry |
| Authorization | Role-based (Applicant / Admin) via JWT claims |
| Real-time updates | SignalR WebSocket for document status |
| File storage | Local filesystem (`/app/wwwroot/uploads/`) |
| Max file size | 5 MB per document |
| Supported formats | PDF, JPG, PNG |
| API Gateway | Ocelot — single entry point for all services |
| Async messaging | RabbitMQ with dead-letter queues and retry logic |
| Persistence | SQL Server — separate database per microservice |
