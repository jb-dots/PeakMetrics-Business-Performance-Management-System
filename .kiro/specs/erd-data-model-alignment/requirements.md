# Requirements Document

## Introduction

PeakMetrics is a Business Performance Management System built with ASP.NET Core 8 MVC and Entity Framework Core. The system tracks KPIs, strategic goals, departments, users, and notifications using a Balanced Scorecard (BSC) framework.

The 1st Deliverables document submitted for IT15 defines an ERD and data dictionary that the current codebase does not fully implement. This feature aligns the codebase data model to match that ERD exactly — introducing new entities, adding missing fields, renaming fields, and updating role values — while preserving all existing functionality and migrating existing data safely.

## Glossary

- **System**: The PeakMetrics ASP.NET Core 8 MVC application.
- **EF_Core**: Entity Framework Core, the ORM used for database access and migrations.
- **Perspective**: One of four Balanced Scorecard quadrants: Financial, Customer, Internal Process, Learning & Growth.
- **Perspectives_Table**: The new normalized `Perspectives` database table with a surrogate primary key and a unique name column.
- **GoalKpis_Table**: The new junction table linking `StrategicGoals` to `Kpis` with a composite primary key (`goal_id`, `kpi_id`).
- **Kpi**: A Key Performance Indicator entity stored in the `Kpis` table.
- **KpiLogEntry**: A single logged measurement for a KPI, stored in the `KpiLogEntries` table.
- **StrategicGoal**: A high-level organizational objective stored in the `StrategicGoals` table.
- **Notification**: An in-app alert sent to a user, stored in the `Notifications` table.
- **AppUser**: An application user stored in the `Users` table.
- **Migration**: An EF Core migration file that applies schema changes to the database.
- **Seed_Data**: Static data inserted via `OnModelCreating` in `AppDbContext` to populate reference tables and demo accounts.
- **Role**: A string value on `AppUser` that controls access. Valid values after alignment: `Super Admin`, `Administrator`, `Manager`, `Staff`, `Executive`.
- **TargetYear**: An integer column on `StrategicGoal` representing the four-digit year by which the goal should be achieved, replacing the former `DueDate` datetime column.
- **Notification_Type**: A string column on `Notification` with values `Alert`, `Info`, or `Warning`, replacing the former `Severity` + `Icon` columns.

---

## Requirements

### Requirement 1: Introduce the Perspectives Reference Table

**User Story:** As a developer, I want a normalized `Perspectives` table, so that perspective values are consistent, reusable, and enforced by a foreign key rather than free-text strings.

#### Acceptance Criteria

1. THE System SHALL create a `Perspectives` table with columns: `perspective_id` (integer, PK, auto-increment), `name` (varchar(50), NOT NULL, UNIQUE).
2. THE System SHALL seed the `Perspectives` table with exactly four rows: `Financial`, `Customer`, `Internal Process`, `Learning & Growth`.
3. WHEN EF_Core generates the `Perspective` model class, THE System SHALL map it to the `Perspectives` table with `Id` as the primary key and `Name` as a required, unique string of maximum 50 characters.
4. THE System SHALL expose a `DbSet<Perspective>` named `Perspectives` on `AppDbContext`.
5. WHEN a `Perspective` row is deleted, THE System SHALL prevent deletion if any `Kpi` or `StrategicGoal` references it (restrict delete behavior).

---

### Requirement 2: Replace Perspective String on Kpi with a Foreign Key

**User Story:** As a developer, I want `Kpi.Perspective` to be a foreign key to the `Perspectives` table, so that KPI perspective values are normalized and validated at the database level.

#### Acceptance Criteria

1. THE System SHALL add a `PerspectiveId` integer column (NOT NULL, FK → `Perspectives.perspective_id`) to the `Kpis` table.
2. THE System SHALL remove the `Perspective` free-text string column from the `Kpis` table.
3. WHEN EF_Core scaffolds the `Kpi` model, THE System SHALL replace the `string Perspective` property with `int PerspectiveId` and a `Perspective Perspective` navigation property.
4. THE System SHALL include a `Perspective` navigation property on `Kpi` with an `Include` path available for queries.
5. WHEN the `Kpis` seed data is applied, THE System SHALL map each existing KPI's perspective string to the corresponding `PerspectiveId` integer from the seeded `Perspectives` table.
6. WHEN a view or controller reads a KPI's perspective label, THE System SHALL resolve the label through `Kpi.Perspective.Name` rather than a raw string property.

---

### Requirement 3: Replace Perspective String on StrategicGoal with a Foreign Key

**User Story:** As a developer, I want `StrategicGoal.Perspective` to be a foreign key to the `Perspectives` table, so that goal perspective values are normalized and consistent with KPI perspectives.

#### Acceptance Criteria

1. THE System SHALL add a `PerspectiveId` integer column (NOT NULL, FK → `Perspectives.perspective_id`) to the `StrategicGoals` table.
2. THE System SHALL remove the `Perspective` free-text string column from the `StrategicGoals` table.
3. WHEN EF_Core scaffolds the `StrategicGoal` model, THE System SHALL replace the `string Perspective` property with `int PerspectiveId` and a `Perspective Perspective` navigation property.
4. WHEN a view or controller reads a strategic goal's perspective label, THE System SHALL resolve the label through `StrategicGoal.Perspective.Name`.

---

### Requirement 4: Introduce the GoalKpis Junction Table

**User Story:** As a developer, I want a `GoalKpis` junction table linking strategic goals to KPIs, so that the many-to-many relationship defined in the ERD is enforced at the database level.

#### Acceptance Criteria

1. THE System SHALL create a `GoalKpis` table with columns: `goal_id` (integer, FK → `StrategicGoals.Id`) and `kpi_id` (integer, FK → `Kpis.Id`).
2. THE System SHALL define a composite primary key on `GoalKpis` using (`goal_id`, `kpi_id`).
3. WHEN a `StrategicGoal` is deleted, THE System SHALL cascade-delete all related `GoalKpis` rows.
4. WHEN a `Kpi` is deleted, THE System SHALL cascade-delete all related `GoalKpis` rows.
5. THE System SHALL configure the many-to-many relationship between `StrategicGoal` and `Kpi` through the `GoalKpis` join entity using EF Core's `UsingEntity` fluent API.
6. THE System SHALL expose `ICollection<Kpi> LinkedKpis` on `StrategicGoal` and `ICollection<StrategicGoal> LinkedGoals` on `Kpi` as navigation properties for the relationship.

---

### Requirement 5: Add Missing Fields to the Kpi Model

**User Story:** As a developer, I want the `Kpi` model to include `Frequency`, `Status`, and `CreatedByUserId` fields, so that the entity matches the data dictionary from the deliverables.

#### Acceptance Criteria

1. THE System SHALL add a `Frequency` column (varchar(20), NOT NULL, default `Monthly`) to the `Kpis` table with allowed values: `Monthly`, `Quarterly`, `Annual`.
2. THE System SHALL add a `Status` column (varchar(20), NOT NULL, default `On Track`) to the `Kpis` table with allowed values: `On Track`, `At Risk`, `Behind`.
3. THE System SHALL add a `CreatedByUserId` integer column (nullable, FK → `Users.Id`) to the `Kpis` table.
4. WHEN a `AppUser` referenced by `Kpi.CreatedByUserId` is deleted, THE System SHALL set `Kpi.CreatedByUserId` to NULL (set-null delete behavior).
5. WHEN EF_Core scaffolds the `Kpi` model, THE System SHALL add `string Frequency`, `string Status`, and `int? CreatedByUserId` properties along with an `AppUser? CreatedBy` navigation property.
6. THE System SHALL configure `Frequency` with a maximum length of 20 characters and `Status` with a maximum length of 20 characters in the EF Core model configuration.

---

### Requirement 6: Replace StrategicGoal.DueDate with TargetYear

**User Story:** As a developer, I want `StrategicGoal.DueDate` replaced by `TargetYear` (integer), so that the model matches the ERD which stores only the target year rather than a full date.

#### Acceptance Criteria

1. THE System SHALL remove the `DueDate` datetime column from the `StrategicGoals` table.
2. THE System SHALL add a `TargetYear` integer column (nullable) to the `StrategicGoals` table.
3. WHEN EF_Core scaffolds the `StrategicGoal` model, THE System SHALL replace the `DateTime? DueDate` property with `int? TargetYear`.
4. WHEN a view displays a strategic goal's target, THE System SHALL render `TargetYear` as a four-digit integer (e.g., `2026`) rather than a formatted date string.
5. WHEN a form accepts a strategic goal's target, THE System SHALL accept a four-digit integer input for `TargetYear` rather than a date picker.
6. IF `TargetYear` is provided and is less than 2000 or greater than 2100, THEN THE System SHALL reject the value with a validation error message.

---

### Requirement 7: Replace Notification.Severity and Notification.Icon with Notification.Type

**User Story:** As a developer, I want `Notification.Severity` and `Notification.Icon` replaced by a single `Type` column, so that the notification model matches the ERD's data dictionary.

#### Acceptance Criteria

1. THE System SHALL remove the `Severity` varchar column from the `Notifications` table.
2. THE System SHALL remove the `Icon` varchar column from the `Notifications` table.
3. THE System SHALL add a `Type` column (varchar(20), NOT NULL, default `Info`) to the `Notifications` table with allowed values: `Alert`, `Info`, `Warning`.
4. WHEN EF_Core scaffolds the `Notification` model, THE System SHALL replace the `string Severity` and `string Icon` properties with a single `string Type` property.
5. WHEN the controller creates a notification for a KPI that is `Behind`, THE System SHALL set `Type` to `Alert`.
6. WHEN the controller creates a notification for a KPI that is `At Risk`, THE System SHALL set `Type` to `Warning`.
7. WHEN the controller creates a notification for any other event, THE System SHALL set `Type` to `Info`.
8. WHEN a view renders a notification's visual indicator (icon or badge color), THE System SHALL derive the icon class and color from `Notification.Type` using a deterministic mapping: `Alert` → danger/red, `Warning` → warning/yellow, `Info` → info/blue.

---

### Requirement 8: Add Notification.KpiId Foreign Key

**User Story:** As a developer, I want `Notification.KpiId` as a nullable foreign key to `Kpis`, so that a notification can be traced back to the KPI that triggered it.

#### Acceptance Criteria

1. THE System SHALL add a `KpiId` integer column (nullable, FK → `Kpis.Id`) to the `Notifications` table.
2. WHEN a `Kpi` referenced by `Notification.KpiId` is deleted, THE System SHALL set `Notification.KpiId` to NULL (set-null delete behavior).
3. WHEN EF_Core scaffolds the `Notification` model, THE System SHALL add an `int? KpiId` property and a `Kpi? Kpi` navigation property.
4. WHEN the controller creates a KPI-triggered notification, THE System SHALL populate `Notification.KpiId` with the ID of the triggering KPI.
5. WHEN the controller creates a non-KPI notification (e.g., system alert), THE System SHALL leave `Notification.KpiId` as NULL.

---

### Requirement 9: Align AppUser Role Values

**User Story:** As a developer, I want the `AppUser.Role` values to match the ERD's defined role set, so that role-based access control uses consistent, spec-compliant identifiers.

#### Acceptance Criteria

1. THE System SHALL recognize exactly five valid role values: `Super Admin`, `Administrator`, `Manager`, `Staff`, `Executive`.
2. THE System SHALL update the seeded `AppUser` with `Role = "Admin"` to `Role = "Super Admin"`.
3. THE System SHALL update all seeded `AppUser` records with `Role = "User"` to `Role = "Staff"`.
4. WHEN the controller evaluates role-based access, THE System SHALL use `Super Admin` in place of `Admin` and `Staff` in place of `User` for all role comparisons and constants.
5. WHEN the Dashboard action routes by role, THE System SHALL route `Super Admin` to the Super Admin dashboard builder and `Staff` to the Staff dashboard builder.
6. THE System SHALL update the `AppUser.Role` default value from `"User"` to `"Staff"` in the model definition.
7. WHEN a view displays a user's role label, THE System SHALL render the updated role strings (`Super Admin`, `Staff`) rather than the legacy values.

---

### Requirement 10: Produce an EF Core Migration for All Schema Changes

**User Story:** As a developer, I want a single EF Core migration that applies all ERD alignment changes atomically, so that the database schema can be updated and rolled back reliably.

#### Acceptance Criteria

1. THE System SHALL produce a valid EF Core migration named `ErdAlignment` that encodes all schema changes from Requirements 1–9.
2. WHEN the migration is applied (`dotnet ef database update`), THE System SHALL create the `Perspectives` table and seed its four rows before adding FK columns that reference it.
3. WHEN the migration is applied, THE System SHALL populate `Kpis.PerspectiveId` and `StrategicGoals.PerspectiveId` from the existing string values before dropping the old `Perspective` string columns.
4. WHEN the migration is applied, THE System SHALL populate `StrategicGoals.TargetYear` from the year component of the existing `DueDate` values before dropping the `DueDate` column.
5. WHEN the migration is applied, THE System SHALL populate `Notifications.Type` from the existing `Severity` values using the mapping `Critical` → `Alert`, `Warning` → `Warning`, `Standard` → `Info` before dropping `Severity` and `Icon`.
6. WHEN the migration is rolled back (`dotnet ef database update <previous>`), THE System SHALL restore the schema to its pre-alignment state without data loss for columns that had data.
7. IF the migration is applied to a database that already contains the `Perspectives` table, THEN THE System SHALL skip the table creation step without error (idempotent guard).

---

### Requirement 11: Update AppDbContext Configuration for All New Relationships

**User Story:** As a developer, I want `AppDbContext.OnModelCreating` updated to configure all new entities and relationships, so that EF Core generates correct SQL and enforces all constraints defined in the ERD.

#### Acceptance Criteria

1. THE System SHALL add fluent API configuration for the `Perspective` entity: primary key, `Name` max length 50, unique index on `Name`.
2. THE System SHALL update the `Kpi` fluent configuration to map `PerspectiveId` as a required FK with restrict-on-delete behavior toward `Perspectives`.
3. THE System SHALL update the `Kpi` fluent configuration to add `Frequency` (max length 20, required) and `Status` (max length 20, required) property mappings.
4. THE System SHALL update the `Kpi` fluent configuration to add `CreatedByUserId` as a nullable FK with set-null-on-delete behavior toward `Users`.
5. THE System SHALL add fluent API configuration for the `GoalKpis` join entity with a composite PK and cascade-delete from both sides.
6. THE System SHALL update the `StrategicGoal` fluent configuration to map `PerspectiveId` as a required FK with restrict-on-delete behavior toward `Perspectives`.
7. THE System SHALL update the `Notification` fluent configuration to replace `Severity` (max 50) and `Icon` (max 100) property mappings with `Type` (max 20, required).
8. THE System SHALL update the `Notification` fluent configuration to add `KpiId` as a nullable FK with set-null-on-delete behavior toward `Kpis`.
9. THE System SHALL update the `Perspectives` seed data block to insert the four perspective rows with stable integer IDs (1–4) so that FK seed data on `Kpis` and `StrategicGoals` can reference them deterministically.

---

### Requirement 12: Update All Affected Views and Controllers

**User Story:** As a developer, I want all views and controller code that reference renamed or removed fields to be updated, so that the application compiles and runs correctly after the data model changes.

#### Acceptance Criteria

1. WHEN a view references `Kpi.Perspective` as a string, THE System SHALL update it to use `Kpi.Perspective.Name` (via the navigation property).
2. WHEN a view references `StrategicGoal.Perspective` as a string, THE System SHALL update it to use `StrategicGoal.Perspective.Name`.
3. WHEN a view references `StrategicGoal.DueDate`, THE System SHALL update it to reference `StrategicGoal.TargetYear`.
4. WHEN a view references `Notification.Severity` or `Notification.Icon`, THE System SHALL update it to derive the equivalent display value from `Notification.Type`.
5. WHEN a controller action filters or groups KPIs by perspective, THE System SHALL join through `Kpi.Perspective.Name` rather than the former string column.
6. WHEN a controller action creates or updates a `Kpi`, THE System SHALL accept a `PerspectiveId` integer from the form and validate it against the seeded `Perspectives` table.
7. WHEN a controller action creates or updates a `StrategicGoal`, THE System SHALL accept a `PerspectiveId` integer from the form and validate it against the seeded `Perspectives` table.
8. WHEN a controller action creates a `Notification` for a KPI event, THE System SHALL set `Type` using the mapping defined in Requirement 7 and populate `KpiId`.
9. WHEN the Dashboard action routes by role, THE System SHALL compare against `Super Admin` and `Staff` instead of `Admin` and `User`.
10. WHEN a controller uses `HasAccess(...)` or equivalent role checks, THE System SHALL replace `"Admin"` with `"Super Admin"` and `"User"` with `"Staff"` in all role argument lists.
