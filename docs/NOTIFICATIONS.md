# 🔔 Notification System Documentation

## Overview

The expense tracker sends **in-app notifications** to users via a bell icon in the frontend header. Notifications are localized in **English (en)**, **Japanese (ja)**, and **Myanmar (my)** based on the user's `locale` setting in their profile.

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

### 1. ⚠️ `BUDGET_THRESHOLD_REACHED`

| Field         | Value |
|---------------|-------|
| **When**      | User creates/updates a **COMPLETED expense** and the budget category spend reaches the `AlertThreshold` (default 80%) |
| **Where**     | `TranactionService.CreateTranactionAsync()` / `UpdateTranactionAsync()` → `CheckBudgetAlertAsync()` |
| **Trigger**   | `SpentAmount ≥ AllocatedAmount × AlertThreshold` |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"You've spent 82% of your Food budget (¥41,000 / ¥50,000)"* |
| **Reference** | `budgetCategoryId` → `budget` |

---

### 2. 🚨 `BUDGET_EXCEEDED`

| Field         | Value |
|---------------|-------|
| **When**      | User creates/updates a **COMPLETED expense** and the budget category spend exceeds 100% |
| **Where**     | `TranactionService` → `CheckBudgetAlertAsync()` |
| **Trigger**   | `SpentAmount > AllocatedAmount` |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"Food budget exceeded! Spent ¥55,000 of ¥50,000"* |
| **Reference** | `budgetCategoryId` → `budget` |

---

### 3. 📅 `RECURRING_PAYMENT_DUE`

| Field         | Value |
|---------------|-------|
| **When**      | A recurring payment's `NextDueDate` is within **3 days** from today |
| **Where**     | `NotificationBackgroundService.CheckRecurringPaymentsDueAsync()` |
| **Trigger**   | `NextDueDate ≥ today AND NextDueDate ≤ today + 3 days` (Active status only) |
| **Schedule**  | ⏰ Background job — **every 6 hours** |
| **Example**   | *"Netflix (¥1,500) is due on 2026-04-06"* |
| **Reference** | `recurringId` → `recurring_payment` |

---

### 4. ❗ `RECURRING_PAYMENT_OVERDUE`

| Field         | Value |
|---------------|-------|
| **When**      | A recurring payment is past due and `AutoPay = false` |
| **Where**     | `RecurringPaymentService.ProcessOverduePaymentsAsync()` |
| **Trigger**   | `NextDueDate < today` and not auto-pay → `MissedCount++` |
| **Schedule**  | ⏰ Background job — **every 1 hour** (via `RecurringPaymentBackgroundService`) |
| **Example**   | *"Netflix payment is overdue (missed 2 time(s))"* |
| **Reference** | `recurringId` → `recurring_payment` |

---

### 5. ✅ `RECURRING_PAYMENT_AUTO_PAID`

| Field         | Value |
|---------------|-------|
| **When**      | A recurring payment is past due and `AutoPay = true` — system auto-creates a transaction |
| **Where**     | `RecurringPaymentService.ProcessOverduePaymentsAsync()` |
| **Trigger**   | `NextDueDate < today` and `AutoPay = true` |
| **Schedule**  | ⏰ Background job — **every 1 hour** (via `RecurringPaymentBackgroundService`) |
| **Example**   | *"Netflix (¥1,500) was automatically paid"* |
| **Reference** | `recurringId` → `recurring_payment` |

---

### 6. 🎉 `SAVING_GOAL_REACHED`

| Field         | Value |
|---------------|-------|
| **When**      | A deposit contribution makes `CurrentAmount ≥ TargetAmount` |
| **Where**     | `SavingGoalService.AddContributionAsync()` |
| **Trigger**   | `CurrentAmount ≥ TargetAmount` after deposit (status auto-set to Completed) |
| **Real-time** | ✅ Immediate (on contribution) |
| **Example**   | *"You've reached your \"New Car\" saving goal!"* |
| **Reference** | `savingGoalId` → `saving_goal` |

---

### 7. ⏰ `SAVING_GOAL_DEADLINE_NEAR`

| Field         | Value |
|---------------|-------|
| **When**      | An active saving goal's `TargetDate` is within **7 days** and `CurrentAmount < TargetAmount` |
| **Where**     | `NotificationBackgroundService.CheckSavingGoalDeadlinesAsync()` |
| **Trigger**   | `TargetDate ≥ today AND TargetDate ≤ today + 7 days` (Active + not reached) |
| **Schedule**  | ⏰ Background job — **every 6 hours** |
| **Example**   | *"\"New Car\" goal deadline is in 5 day(s) (¥180,000 / ¥200,000)"* |
| **Reference** | `savingGoalId` → `saving_goal` |

---

### 8. ❌ `PAYMENT_FAILED`

| Field         | Value |
|---------------|-------|
| **When**      | A transaction is created with `Status = FAILED`, or updated from any status → `FAILED` |
| **Where**     | `TranactionService.CreateTranactionAsync()` / `UpdateTranactionAsync()` |
| **Trigger**   | `Status == Failed` on create, or `oldStatus != Failed && newStatus == Failed` on update |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"Transaction \"Electricity Bill\" (¥8,000) failed"* |
| **Reference** | `transactionId` → `transaction` |

---

### 9. 💰 `LARGE_TRANSACTION`

| Field         | Value |
|---------------|-------|
| **When**      | A **COMPLETED expense** amount ≥ user's `DailyLimit` from Profile Settings |
| **Where**     | `TranactionService.CreateTranactionAsync()` → `CheckDailyLimitAsync()` |
| **Trigger**   | `tx.Amount ≥ profile.DailyLimit` (only if DailyLimit > 0) |
| **Real-time** | ✅ Immediate (on transaction) |
| **Example**   | *"¥5,000 expense recorded for \"Dinner\""* (DailyLimit = ¥2,000) |
| **Reference** | `transactionId` → `transaction` |
| **Setting**   | Configured in **Profile Settings → Daily Spending Limit** |

---

### 10. 📊 `EXPORT_COMPLETED` / ❌ `EXPORT_FAILED`

| Field         | Value |
|---------------|-------|
| **When**      | Lambda finishes processing the Excel export |
| **Where**     | ⏳ Not yet wired — requires Lambda callback endpoint |
| **Example**   | *"Your transaction export (Jan–Mar 2026) is ready to download"* |
| **Reference** | `exportJobId` → `export` |

---

## Background Services Schedule

| Service | Interval | What it checks |
|---------|----------|----------------|
| `RecurringPaymentBackgroundService` | **Every 1 hour** | Overdue payments → auto-pay or notify overdue |
| `NotificationBackgroundService` | **Every 6 hours** | Upcoming due (3 days) + Goal deadlines (7 days) |

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
  ├── Due within 3 days        → RECURRING_PAYMENT_DUE
  └── Deadline within 7 days   → SAVING_GOAL_DEADLINE_NEAR
```

---

## Localization (Resource Files)

All notification titles and messages are in `SharedResource.{locale}.resx`:

| File | Language |
|------|----------|
| `Resources/SharedResource.resx` | Default (English) |
| `Resources/SharedResource.en.resx` | English |
| `Resources/SharedResource.ja.resx` | Japanese |
| `Resources/SharedResource.my.resx` | Myanmar |

Resource keys:
- **Titles**: `Notif_{Type}_Title` — e.g. `Notif_BudgetThreshold_Title`
- **Messages**: `Notif_{Type}_Msg` — e.g. `Notif_BudgetThreshold_Msg` with `{0}`, `{1}` placeholders

### Notification Preferences

Users can toggle individual notification categories on/off in **Profile Settings**:

```http
PUT /api/profile
{
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

### Deduplication

Background service notifications (`RECURRING_PAYMENT_DUE`, `SAVING_GOAL_DEADLINE_NEAR`) check if a notification with the same `type + referenceId` already exists today before creating a new one. This prevents duplicate spam.

---

## Frontend Integration

```
🔔 Bell icon (header)
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
```
