# Law Firm Case Management System

## Project Overview
The **Law Firm Case Management System** is a Windows desktop application designed specifically for legal teams. It provides a comprehensive suite of tools to manage case files, client records, team tasks, and documents efficiently. The system is built with role-based access control, catering to Admins, Lawyers, and Paralegals, ensuring that data visibility and actions are restricted based on user roles.

## Tech Stack
The project is built using modern Microsoft technologies for robust desktop application development:
- **Presentation Layer**: WPF (Windows Presentation Foundation) for a rich desktop UI.
- **Framework**: .NET (C#)
- **Data Access**: Entity Framework Core (EF Core) as the ORM.
- **Architecture**: MVVM (Model-View-ViewModel) pattern, segregating the UI from the business logic.

## Database
- **Primary / Fallback Database**: **SQLite** (a local `law_case_management.db` database is used for local fallback or standalone setups).
- **Network Database**: Supports **SQL Server Express** for shared network database configuration.

## Features Implemented

### 1. User Authentication & Role Management
- Secure login system utilizing BCrypt for password hashing.
- Role-based session management with distinct permissions for Admins, Lawyers, and Paralegals.
- User account administration and status toggling (enable/disable users).
- **Session Timeout Manager**: Auto-logout mechanism that monitors global user inactivity (mouse/keyboard events) and returns the user to the Login window after an idle period.

### 2. Case File Management
- Creation and management of case records.
- **Auto-generated File Numbers**: Formatted as `{seq}/{year}` (e.g., `111/2026`).
- Comprehensive case details tracking: Title, Type (Civil, Criminal, Family, Corporate, Other), Status (Open, In Progress, Closed, Archived), Dates (Opened, Closed), Notes, and Staff Assignments (Lawyer + Paralegal).
- Many-to-many relationship management between Cases and Clients.

### 3. Client Records
- Management of detailed client contact records (Full Name, Address, Phone, Email).
- Robust search functionality across name, phone, address, and email fields.
- **Linked Cases Panel**: Real-time display of all active and historical cases associated with a selected client.

### 4. Task Management
- Creation and tracking of tasks linked to specific cases and staff members.
- Multi-criteria filtering by Staff, Case, and Status.
- Status lifecycle progression (Pending -> In Progress -> Completed).
- Quick task creation directly from the Tasks View or within a Case File's details.

### 5. Document Management
- Attachment of documents to case files.
- File size validation (limit of 50 MB per document).
- **Native Document Launch**: Ability to open/launch document files directly from the UI using the system's default native handlers (e.g., Word, PDF reader, Excel).
- Role-restricted document deletion (allowed only for Admins and Lawyers).

### 6. Dashboard & Overview
- High-level overview with key case metric counters (Total, Open, In Progress, Closed/Archived).
- "My Cases" and "My Tasks" widgets, dynamically tailored to the currently logged-in user.
- Team activity audit trail visibility.

### 7. System & Audit Security
- Comprehensive audit logging for all critical operations, including user logins/logouts, case creation/edits, client updates, task assignments, and document operations.

## Routing and Navigation
Given that this is a WPF desktop application using the MVVM pattern, "routing" is handled via view switching rather than traditional web URLs.
- **MainViewModel Navigation**: Acts as the central hub for view switching. It dynamically swaps the active ViewModel (e.g., switching from `DashboardViewModel` to `CasesViewModel`).
- **Cross-View Jumping**: The system supports smooth contextual navigation triggers. For example, a user can click on a case in the Dashboard's "My Cases" widget or a client's "Linked Cases" list and jump directly into the Case detail view with that specific case fully loaded and ready for interaction.
