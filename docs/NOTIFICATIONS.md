# Notification System Documentation

## Overview

The expense tracker sends **in-app notifications** (bell icon) and optional **email notifications** (SMTP). Both are localized in **English (en)**, **Japanese (ja)**, and **Myanmar (my)** based on the user's `locale`. Email subject/body templates are edited in `appsettings.json` under `Email:Templates` (not `.resx`).

Menu entry for the frontend: key `emailSent`, path `/email-sent` (settings + sent history).

---

## Localization

Notifications are stored in the database **already translated** into the user's preferred language. The `Localize()` method explicitly sets `CultureInfo.CurrentUICulture` to the user's saved `locale` (from `member_profiles.locale`) before reading from `SharedResource.{locale}.resx`.

```
User profile: locale = "my"
  → Localize("my", "Notif_RecurringDue_Title")
  → CultureInfo.CurrentUICulture = "my"
  → _localizer reads SharedResource.my.resx
  → "📅 ပေးချေရန် နီးပြီ"
  → saved to notifications table in Myanmar ✅
```

### Setting locale

```http
PUT /api/profile
{ "locale": "ja" }
```

Supported values: `"en"`, `"ja"`, `"my"`

---

## Database Table

```
notifications
├── id              UUID (PK)
├── user_id         VARCHAR(50)   — owner
├── type            VARCHAR(50)   — e.g. BUDGET_EXCEEDED
├── title           VARCHAR(200)  — localized short title
├── message         VARCHAR(1000) — localized detail message
├── reference_id    VARCHAR(50)   — related entity ID (nullable)
├── reference_type  VARCHAR(30)   — "budget", "recurring_payment", etc.
├── is_read         BOOLEAN       — false = unread
├── created_at      TIMESTAMP     — when created
├── read_at         TIMESTAMP     — when marked read (nullable)

Indexes: (user_id, is_read), (user_id, created_at)
```

---

## API Endpoints

| Method    | Endpoint                            | Purpose                         |
|-----------|-------------------------------------|---------------------------------|
| `GET`     | `/api/notifications/summary`        | Unread count + 5 latest (bell)  |
| `GET`     | `/api/notifications/unread-count`   | Badge number only               |
| `GET`     | `/api/notifications`                | Full list (paginated)           |
| `PATCH`   | `/api/notifications/{id}/read`      | Mark one as read                |
| `PATCH`   | `/api/notifications/read-all`       | Mark all as read                |
| `DELETE`  | `/api/notifications/{id}`           | Delete one                      |
| `DELETE`  | `/api/notifications/read`           | Delete all read notifications   |

### Query Parameters for `GET /api/notifications`

| Param      | Type       | Default | Description                   |
|------------|------------|---------|-------------------------------|
| `isRead`   | `bool?`    | null    | Filter: `true`/`false`/all    |
| `pageSize` | `int`      | 20      | 1–50                          |
| `cursor`   | `DateTime?`| null    | Cursor-based pagination       |

---

## Notification Types — When & Where They Fire

### 1. `BUDGET_THRESHOLD_REACHED`

| Field         | Value |
|---------------|-------|
| **When**      | User creates/updates a **COMPLETED expense** and the budget category spend reaches the `AlertThreshold` (default 80%), **and** the category has `AlertsEnabled = true` |
| **Where**     | `TranactionService.CreateTranactionAsync()` / `UpdateTranactionAsync()` → `CheckBudgetAlertAsync()` |
| **Trigger**   | `AlertsEnabled` **and** `SpentAmount ≥ AllocatedAmount × AlertThreshold` |
| **Also needs**| Profile `NotifyBudgetAlerts = true` |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"You've spent 82% of your Food budget (¥41,000 / ¥50,000)"* |
| **Reference** | `budgetCategoryId` → `budget` |

---

### 2. `BUDGET_EXCEEDED`

| Field         | Value |
|---------------|-------|
| **When**      | User creates/updates a **COMPLETED expense** and the budget category spend exceeds 100%, **and** the category has `AlertsEnabled = true` |
| **Where**     | `TranactionService` → `CheckBudgetAlertAsync()` |
| **Trigger**   | `AlertsEnabled` **and** `SpentAmount > AllocatedAmount` |
| **Also needs**| Profile `NotifyBudgetAlerts = true` |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"Food budget exceeded! Spent ¥55,000 of ¥50,000"* |
| **Reference** | `budgetCategoryId` → `budget` |

---

### 3. `RECURRING_PAYMENT_DUE`

| Field         | Value |
|---------------|-------|
| **When**      | A recurring payment hits a configured due milestone (default **7, 3, 1** days before, plus **due date**) |
| **Where**     | `NotificationBackgroundService.CheckRecurringPaymentsDueAsync()` |
| **Trigger** | Exact match on `daysUntilDue` ∈ `Email:Timings:RecurringDueDaysBefore` (and day 0 if `RecurringDueOnDueDate`) |
| **Schedule**  | Background job — **every 6 hours** |
| **Example**   | *"Netflix (¥1,500) is due on 2026-04-06"* |
| **Reference** | `recurringId` → `recurring_payment` |
| **Email milestone** | `due_7`, `due_3`, `due_1`, `due_0` (deduped in `email_sent_logs`) |

---

### 4. `RECURRING_PAYMENT_OVERDUE`

| Field         | Value |
|---------------|-------|
| **When**      | A recurring payment is past due and `AutoPay = false` |
| **Where**     | `RecurringPaymentService.ProcessOverduePaymentsAsync()` |
| **Trigger**   | `NextDueDate < today` and not auto-pay → `MissedCount++` |
| **Schedule**  | ⏰ Background job — **every 1 hour** (via `RecurringPaymentBackgroundService`) |
| **Example**   | *"Netflix payment is overdue (missed 2 time(s))"* |
| **Reference** | `recurringId` → `recurring_payment` |

---

### 5. `RECURRING_PAYMENT_AUTO_PAID`

| Field         | Value |
|---------------|-------|
| **When**      | A recurring payment is past due and `AutoPay = true` — system auto-creates a transaction |
| **Where**     | `RecurringPaymentService.ProcessOverduePaymentsAsync()` |
| **Trigger**   | `NextDueDate < today` and `AutoPay = true` |
| **Schedule**  | ⏰ Background job — **every 1 hour** (via `RecurringPaymentBackgroundService`) |
| **Example**   | *"Netflix (¥1,500) was automatically paid"* |
| **Reference** | `recurringId` → `recurring_payment` |

---

### 6. `SAVING_GOAL_REACHED`

| Field         | Value |
|---------------|-------|
| **When**      | A deposit contribution makes `CurrentAmount ≥ TargetAmount` |
| **Where**     | `SavingGoalService.AddContributionAsync()` |
| **Trigger**   | `CurrentAmount ≥ TargetAmount` after deposit (status auto-set to Completed) |
| **Real-time** | ✅ Immediate (on contribution) |
| **Example**   | *"You've reached your \"New Car\" saving goal!"* |
| **Reference** | `savingGoalId` → `saving_goal` |

---

### 7. `SAVING_GOAL_DEADLINE_NEAR`

| Field         | Value |
|---------------|-------|
| **When**      | An active saving goal's `TargetDate` is within `Email:Timings:SavingGoalDeadlineDaysBefore` days (default **7**) and not yet reached |
| **Where**     | `NotificationBackgroundService.CheckSavingGoalDeadlinesAsync()` |
| **Trigger**   | `TargetDate ≥ today AND TargetDate ≤ today + N days` (Active + not reached) |
| **Schedule**  | Background job — **every 6 hours** |
| **Example**   | *"\"New Car\" goal deadline is in 5 day(s) (¥180,000 / ¥200,000)"* |
| **Reference** | `savingGoalId` → `saving_goal` |

---

### 8. `PAYMENT_FAILED`

| Field         | Value |
|---------------|-------|
| **When**      | A transaction is created with `Status = FAILED`, or updated from any status → `FAILED` |
| **Where**     | `TranactionService.CreateTranactionAsync()` / `UpdateTranactionAsync()` |
| **Trigger**   | `Status == Failed` on create, or `oldStatus != Failed && newStatus == Failed` on update |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"Transaction \"Electricity Bill\" (¥8,000) failed"* |
| **Reference** | `transactionId` → `transaction` |

---

### 9. `LARGE_TRANSACTION`

| Field         | Value |
|---------------|-------|
| **When**      | A **COMPLETED expense** amount ≥ user's `DailyLimit` from Profile Settings |
| **Where**     | `TranactionService.CreateTranactionAsync()` → `CheckDailyLimitAsync()` |
| **Trigger**   | `tx.Amount ≥ profile.DailyLimit` (only if DailyLimit > 0) |
| **Real-time** | Immediate (on transaction) |
| **Example**   | *"¥5,000 expense recorded for \"Dinner\""* (DailyLimit = ¥2,000) |
| **Reference** | `transactionId` → `transaction` |
| **Setting**   | Profile → **Daily Spending Limit** (`DailyLimit`). Same rule drives in-app and email. |

---

### 10. `EXPORT_COMPLETED` / `EXPORT_FAILED`

| Field         | Value |
|---------------|-------|
| **When**      | Not wired for production email |
| **Email**     | **Removed** — export emails are not sent |
| **In-app**    | Helpers exist but require a Lambda callback (still not wired) |
| **Frontend**  | Hide “Export Notifications” on Email Sent page |

---

## Background Services Schedule

| Service | Interval | What it checks |
|---------|----------|----------------|
| `RecurringPaymentBackgroundService` | **Every 1 hour** | Overdue payments → auto-pay or notify overdue |
| `NotificationBackgroundService` | **Every 6 hours** | Due milestones (JSON timings) + goal deadlines + flush pending emails |

---

## Timing Summary

```
IMMEDIATE (on user action):
  ├── Create/Update expense (COMPLETED)
  │     ├── Budget ≥ 80%?        → BUDGET_THRESHOLD_REACHED
  │     ├── Budget > 100%?       → BUDGET_EXCEEDED
  │     └── Amount ≥ DailyLimit? → LARGE_TRANSACTION
  │
  ├── Create/Update transaction (FAILED)
  │     └── PAYMENT_FAILED
  │
  └── Saving goal contribution (deposit)
        └── CurrentAmount ≥ Target? → SAVING_GOAL_REACHED

EVERY 1 HOUR (RecurringPaymentBackgroundService):
  ├── Overdue + AutoPay=true   → RECURRING_PAYMENT_AUTO_PAID
  └── Overdue + AutoPay=false  → RECURRING_PAYMENT_OVERDUE

EVERY 6 HOURS (NotificationBackgroundService):
  ├── Due on days 7/3/1/0 (configurable) → RECURRING_PAYMENT_DUE
  ├── Deadline within N days (default 7) → SAVING_GOAL_DEADLINE_NEAR
  └── Flush Pending emails (outside quiet hours)
```

---

## Email channel (SMTP + JSON templates)

### Enable flow

1. Set `Email:Enabled = true` and SMTP host/credentials in `appsettings.json` (or env).
2. User opts in: `notifyEmailEnabled: true` via `PUT /api/profile` or `PUT /api/email-settings`.
3. Category prefs (`notificationPreferences.*`) gate **both** in-app and email.
4. Templates resolve by notification `type` + user `locale` (`en`/`ja`/`my`).

### Config shape (`Email` section)

See [`appsettings.json`](../appsettings.json):

- `Smtp` — host, port, SSL, username/password, from address
- `QuietHours` — UTC hours (default 22–08); emails are queued as `Pending` and flushed later
- `Timings` — bill due days, overdue days after, saving-goal deadline window
- `Templates` — per-type `{ en, ja, my }` with `subject` + `bodyHtml` and `{placeholders}`

### APIs

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/email-sent` | Paginated history (`status`, `pageSize`, `cursor`) |
| `GET` | `/api/email-settings` | Timings + template types + user prefs (no SMTP secrets) |
| `PUT` | `/api/email-settings` | Update `notifyEmailEnabled` + category prefs |

### `email_sent_logs` table

```
email_sent_logs
├── id, user_id, to_address, type, subject, body_html
├── locale, status (Pending/Sent/Failed/Skipped), error
├── reference_id, milestone, created_at, sent_at
```

### Opt-in example

```http
PUT /api/email-settings
{
  "notifyEmailEnabled": true,
  "notificationPreferences": {
    "budgetAlerts": true,
    "recurringPayments": true,
    "autoPayments": true,
    "savingGoals": true,
    "largeTransactions": true,
    "paymentFailures": true,
    "exports": true
  }
}
```

---

## Localization (Resource Files)

In-app notification titles and messages are in `SharedResource.{locale}.resx`:

| File | Language |
|------|----------|
| `Resources/SharedResource.resx` | Default (English) |
| `Resources/SharedResource.en.resx` | English |
| `Resources/SharedResource.ja.resx` | Japanese |
| `Resources/SharedResource.my.resx` | Myanmar |

Resource keys:
- **Titles**: `Notif_{Type}_Title` — e.g. `Notif_BudgetThreshold_Title`
- **Messages**: `Notif_{Type}_Msg` — e.g. `Notif_BudgetThreshold_Msg` with `{0}`, `{1}` placeholders

Email copy is **not** in `.resx` — edit `Email:Templates` in JSON.

### Notification Preferences

Users can toggle individual notification categories on/off in **Profile Settings** (also exposed under Email Sent):

```http
PUT /api/profile
{
  "notifyEmailEnabled": true,
  "notificationPreferences": {
    "budgetAlerts": true,
    "recurringPayments": true,
    "autoPayments": true,
    "savingGoals": true,
    "largeTransactions": true,
    "paymentFailures": true,
    "exports": true
  }
}
```

Budget alerts also require the category’s `alertsEnabled` flag (default `true`). Set `alertsEnabled: false` on fixed bills (electric, rent) so paying the exact allocation does not notify; leave `true` for variable spending (food/groceries).

```http
POST /api/budgets/{budgetId}/categories
{
  "categoryId": "...",
  "allocatedAmount": 4500,
  "alertsEnabled": false
}
```

### Deduplication

Background service notifications (`RECURRING_PAYMENT_DUE`, `SAVING_GOAL_DEADLINE_NEAR`) check if a notification with the same `type + referenceId` already exists today before creating a new one. Email due milestones are also deduped via `email_sent_logs.milestone` (e.g. `due_7`).

---

## Frontend Integration

```
Bell icon (header)
 │
 ├─ On page load → GET /api/notifications/summary
 │    → show red badge with unreadCount
 │    → dropdown shows 5 latest notifications
 │
 ├─ Poll every 30s → GET /api/notifications/unread-count
 │    → update badge number
 │
 ├─ Click notification → PATCH /api/notifications/{id}/read
 │    → navigate to referenced page using referenceType + referenceId
 │
 ├─ "Mark all read" → PATCH /api/notifications/read-all
 │
 └─ "View all" → Notification page → GET /api/notifications?pageSize=20

Email Sent page (/email-sent)
 │
 ├─ GET /api/email-settings → toggles + timings
 ├─ PUT /api/email-settings → save notifyEmailEnabled + prefs
 └─ GET /api/email-sent → history table
```
