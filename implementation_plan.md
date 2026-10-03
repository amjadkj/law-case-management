# Implementation Plan — Law Firm Case Management System (PRD v1.0)

This plan details the full implementation and enhancement of the **Law Firm Case Management System** according to the Product Requirements Document (PRD v1.0).

---

## 1. Executive Overview & Target Scope

The system is a Windows desktop application (WPF / .NET / EF Core) designed for legal teams with role-based access for **Admins**, **Lawyers**, and **Paralegals**.

### PRD Modules & Functional Requirements:
1. **User Authentication & Role Management (Section 4.1)**:
   - BCrypt password hashing & role sessions (Admin, Lawyer, Paralegal).
   - User account administration & status toggling.
   - Inactivity session auto-logout.
2. **Case File Management (Section 4.2)**:
   - Case records with FileNumber formatted as `{number}/{year}` (e.g. `111/2026`), where `{number}` is provided by the user or auto-generated sequentially for the current year (e.g., `1/2026`, `2/2026`, `111/2026`).
   - Title, Type (Civil, Criminal, Family, Corporate, Other), Status (Open, In Progress, Closed, Archived), Dates, Notes, and Staff assignments (Lawyer + Paralegal).
   - Case-Client many-to-many junction management.
   - Role-based permissions across Lawyer, Paralegal, and Admin.
3. **Client Records (Section 4.3)**:
   - Contact records (Full Name, Address, Phone, Email).
   - Search across name, phone, address, email.
   - Real-time display of all **Linked Cases** for a selected client with jump-to-case capability.
4. **Task Management (Section 4.4)**:
   - Multi-criteria filtering by Staff, Case, and Status.
   - Quick status lifecycle progression (Pending $\rightarrow$ In Progress $\rightarrow$ Completed).
   - Create task directly from Tasks View or from within Case File details.
5. **Document Management (Section 4.5)**:
   - Case document attachments with 50 MB limit validation.
   - Open / launch document files directly from the UI using system default handlers.
   - Role-restricted deletion (Admin & Lawyer only).
6. **Dashboard & Overview (Section 4.6)**:
   - Key case metric counters (Total, Open, In Progress, Closed/Archived).
   - "My Cases" and "My Tasks" widgets tailored to logged-in user with quick navigation.
   - Team activity audit trail.
7. **System & Audit Security (Section 5 & 6)**:
   - Audit logging for all critical operations (User login/logout, Case creation/edits, Client updates, Task assignments, Document operations).
   - Database connection configurability (SQL Server Express shared network DB and local SQLite fallback).

---

## 2. Proposed Changes

### Core Layer (`LawCaseManagement.Core`)

#### [MODIFY] [Services.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Core/Services.cs)
- Add auto-generated file number generation utility `GenerateNextFileNumber(string caseType)`.
- Enhance `ClientService` to include retrieval of all cases associated with a client (`GetCasesForClient(int clientId)`).
- Enhance `TaskService` to support retrieving tasks by case, staff member, and status, and adding delete/edit helpers.
- Enhance `DocumentService` with file size validation, role verification, and system process launcher helpers (`OpenDocument(string filePath)`).

#### [MODIFY] [AdminService.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Core/AdminService.cs)
- Ensure robust validation for username uniqueness and password hashing.
- Enhance audit log query filtering.

---

### UI & Presentation Layer (`LawCaseManagement.Wpf`)

#### [NEW] [SessionTimeoutManager.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/SessionTimeoutManager.cs)
- Global user activity monitor (mouse/keyboard events) with a timer that automatically logs the user out upon inactivity and returns to the Login window with a timeout notification.

#### [MODIFY] [MainViewModel.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/ViewModels/MainViewModel.cs)
- Add navigation triggers allowing smooth cross-view jumping (e.g. jumping from Dashboard "My Cases" or Client "Linked Cases" directly into Case detail view with the selected case loaded).

#### [MODIFY] [CasesViewModel.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/ViewModels/CasesViewModel.cs)
- Add auto-generate File Number command.
- Add DateOpened and DateClosed form controls.
- Add staff combo binding for task creation to include all active staff (lawyers and paralegals).
- Add Open Document command to launch files directly in their native viewer (Word, PDF reader, Excel, etc.).
- Add role-based action enforcement (e.g., Paralegals cannot delete documents, only view/update assigned cases).

#### [MODIFY] [ClientsViewModel.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/ViewModels/ClientsViewModel.cs)
- Load and display all **Linked Cases** for the selected client.
- Add "Open Case in Case View" command from the client's linked case list.

#### [MODIFY] [TasksViewModel.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/ViewModels/TasksViewModel.cs)
- Add case-filter combo so tasks can be filtered by specific case.
- Add dialog/form to create a task directly from the Tasks view (with case selection and staff assignment).
- Add delete task command.

#### [MODIFY] [DashboardViewModel.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/ViewModels/DashboardViewModel.cs)
- Enable quick jump actions for "My Cases" and "My Tasks".

#### [MODIFY] [Views/CasesView.xaml](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/Views/CasesView.xaml)
- Add "Auto-generate" button for File Number.
- Add "Open" button next to each document in the document list.
- Fix staff list dropdown in Task creation to show both Lawyers & Paralegals.
- Include Date Opened and Date Closed pickers.

#### [MODIFY] [Views/ClientsView.xaml](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/Views/ClientsView.xaml)
- Add "Linked Cases" panel displaying all active and historical cases for the selected client.

#### [MODIFY] [Views/TasksView.xaml](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/Views/TasksView.xaml)
- Add Case filter and a "New Task" creation panel/dialog.

#### [DELETE] [MainWindow.xaml](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/MainWindow.xaml) & [MainWindow.xaml.cs](file:///d:/amjad/freelance/law-case-management/LawCaseManagement.Wpf/MainWindow.xaml.cs)
- Remove unused root boilerplate window to eliminate naming ambiguity with `Views/MainWindow.xaml`.

---

## 3. Verification Plan

### Manual & Unit Verification:
1. **Authentication & Roles**:
   - Verify login with admin/admin123, sarah/lawyer123, and rachel/paralegal123.
   - Verify Admin sees Admin Panel; Lawyer and Paralegal do not.
   - Verify inactive session logout triggers after idle period.
2. **Case File Management**:
   - Auto-generate file number format `CIV-2026-XXXX`.
   - Create new case with multiple linked clients, lawyer, and paralegal.
   - Add task and upload document (verify file copied to shared folder and size checked).
   - Test "Open Document" opens file in Windows default application.
3. **Client Records**:
   - Select client and verify all linked cases show up immediately.
   - Create new client and verify search works across name, address, phone, email.
4. **Task Management**:
   - Filter by status (Pending, In Progress, Completed), Staff, and Case.
   - Cycle task status and verify persistence in DB.
5. **Dashboard**:
   - Verify counts match actual database counts (Open, In Progress, Closed).
   - Verify My Cases and My Tasks show items assigned to current user.
   - Verify Recent Activity logs all user actions.

## 4. Deployment & Release

- **CI/CD Pipeline**: Set up GitHub Actions to run unit tests, build the project, and create a Windows installer (MSI) on each push to `main`.
- **Package Generation**: Use WiX Toolset to bundle the WPF application, .NET runtime, and required SQLite/SQL Server Express drivers.
- **Environment Configuration**: Provide separate `appsettings.Development.json` and `appsettings.Production.json` for database connection strings.
- **Release Process**:
  1. Tag a release commit (e.g., `v1.0.0`).
  2. GitHub Action publishes the MSI to the repository releases page.
  3. Internal QA downloads the installer and runs the verification plan.

## 5. Project Timeline (8‑week sprint)

| Week | Milestones |
|------|------------|
| 1 | Project scaffolding – solution, CI pipeline, core data models, authentication service. |
| 2 | Implement case file management service and auto‑generation of file numbers. |
| 3 | Client service enhancements and many‑to‑many client‑case linking UI. |
| 4 | Task service, UI for task creation, filtering, and status workflow. |
| 5 | Document service – upload, size validation, open‑document command, role restrictions. |
| 6 | Dashboard widgets, audit logging, session timeout manager. |
| 7 | End‑to‑end manual verification, bug‑fixing, performance tuning. |
| 8 | Release preparation, installer generation, final QA sign‑off. |

## 6. Risks & Mitigations

- **Data loss / corruption**: Implement nightly automated DB backups and transaction‑scoped EF Core operations.
- **Performance degradation with large document sets**: Lazy‑load document collections and paginate UI lists.
- **Security (password leakage)**: Enforce BCrypt with cost factor ≥12, never log plaintext passwords.
- **Concurrency conflicts**: Use optimistic concurrency tokens in EF Core for case and task updates.
- **User adoption**: Provide in‑app onboarding tour and comprehensive help documentation.

---

## 7. Future Enhancements

- **Outlook / Calendar Integration**: Sync task deadlines and case events with Microsoft Outlook calendar for reminders.
- **Cross‑Platform Mobile Companion**: Leverage .NET MAUI to provide a lightweight mobile app for case lookup and task updates.
- **Advanced Search Engine**: Implement ElasticSearch for full‑text indexing of documents, notes, and client information.
- **Customizable Dashboards**: Allow admins to configure widgets per role (e.g., KPI charts, recent activity streams).
- **AI‑Assisted Drafting**: Integrate a language model to suggest clause drafts based on case type.

## 8. Acceptance Criteria

| # | Criteria | Verification Method |
|---|----------|----------------------|
| 1 | All core modules (Auth, Cases, Clients, Tasks, Documents) functional | Automated unit & integration test suite (≥90% coverage) |
| 2 | Role‑based UI restrictions enforced | UI automated tests with WinAppDriver |
| 3 | Auto‑generated file numbers follow `{seq}/{year}` format | Manual test case creating multiple cases in same year |
| 4 | Document upload size limit 50 MB and opening works | Manual upload + OS file‑open verification |
| 5 | Session timeout logs out after 15 min inactivity | Automated UI idle simulation |
| 6 | Deployment installer installs, runs, and launches app without errors | QA install test on clean Windows VM |
| 7 | Audit logs capture all critical actions with correct timestamps | Log inspection after functional tests |

---
dotnet run --project LawCaseManagement.Wpf/LawCaseManagement.Wpf.csproj

dotnet watch --project LawCaseManagement.Wpf/LawCaseManagement.Wpf.csproj