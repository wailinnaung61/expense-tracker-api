using OpenAI.Chat;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatToolRegistry
{
    public List<ChatTool> BuildAll()
    {
        var tools = new List<ChatTool>();
        tools.AddRange(TransactionTools());
        tools.AddRange(CategoryTools());
        tools.AddRange(BudgetTools());
        tools.AddRange(RecurringPaymentTools());
        tools.AddRange(SavingGoalTools());
        tools.AddRange(InvestmentTools());
        tools.AddRange(ReportTools());
        return tools;
    }

    private static List<ChatTool> TransactionTools() =>
    [
        ChatTool.CreateFunctionTool(
            "add_expense",
            "Add a new expense transaction. Use when user wants to record spending money.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "amount": { "type": "number", "description": "The expense amount" },
                    "description": { "type": "string", "description": "What the expense is for" },
                    "category": { "type": "string", "description": "Category name like Food, Transport, Shopping. Backend resolves to ID." },
                    "category_id": { "type": "string", "description": "Category UUID if already known from context. Prefer passing category name." },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd format. Use today if not specified." },
                    "status": { "type": "string", "enum": ["Completed", "Pending"], "description": "Payment status, default Completed" }
                },
                "required": ["amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "add_income",
            "Add a new income transaction. Use when user mentions receiving money, salary, freelance payment.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "amount": { "type": "number", "description": "The income amount" },
                    "description": { "type": "string", "description": "Income source description" },
                    "category": { "type": "string", "description": "Category name like Salary, Freelance. Backend resolves to ID." },
                    "category_id": { "type": "string", "description": "Category UUID if already known" },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd format" },
                    "status": { "type": "string", "enum": ["Completed", "Pending"] }
                },
                "required": ["amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "add_investment",
            "Add a new investment transaction. Use when user mentions stocks, crypto, funds.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "amount": { "type": "number", "description": "The investment amount" },
                    "description": { "type": "string", "description": "Investment description" },
                    "category": { "type": "string", "description": "Category name. Backend resolves to ID." },
                    "category_id": { "type": "string", "description": "Category UUID if already known" },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd format" }
                },
                "required": ["amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "add_savings",
            "Add a new savings transaction. Use when user mentions saving, deposit to savings.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "amount": { "type": "number", "description": "The savings amount" },
                    "description": { "type": "string", "description": "Savings description" },
                    "category": { "type": "string", "description": "Category name. Backend resolves to ID." },
                    "category_id": { "type": "string", "description": "Category UUID if already known" },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd format" }
                },
                "required": ["amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_transaction",
            "Update an existing transaction. Pass name/amount/date to locate it — backend resolves. No need for IDs.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "transaction_id": { "type": "string", "description": "Transaction UUID if known (optional)" },
                    "old_amount": { "type": "number", "description": "Current amount before update — used to locate the transaction" },
                    "match_description": { "type": "string", "description": "Current description keyword to locate transaction (e.g. 'Taxi', 'Netflix')" },
                    "match_date": { "type": "string", "description": "Current date in yyyy-MM-dd to locate transaction" },
                    "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Transaction type for matching" },
                    "new_amount": { "type": "number", "description": "New amount after update" },
                    "amount": { "type": "number", "description": "New amount (alias of new_amount)" },
                    "description": { "type": "string", "description": "New description" },
                    "category": { "type": "string", "description": "New category name. Backend resolves." },
                    "category_id": { "type": "string", "description": "New category UUID if known" },
                    "date": { "type": "string", "description": "New date in yyyy-MM-dd format" },
                    "status": { "type": "string", "enum": ["Completed", "Pending"] }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "list_transactions",
            "List recent transactions. Use when user asks to see or show expenses, income, or transactions.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Filter by type" },
                    "start_date": { "type": "string", "description": "Start date yyyy-MM-dd. For 'this month' use first day of current month." },
                    "end_date": { "type": "string", "description": "End date yyyy-MM-dd. For 'this month' use last day of current month." },
                    "keyword": { "type": "string", "description": "Search keyword in description" },
                    "limit": { "type": "integer", "description": "Number of transactions to show, default 10" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "find_transaction",
            "Search for transactions by amount, description, date, or type. Use before deleting to show matches and ask for confirmation.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "amount": { "type": "number", "description": "The transaction amount to match" },
                    "description": { "type": "string", "description": "Keyword to search in description" },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd format" },
                    "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"] }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_transaction",
            "Delete a transaction by its exact ID. Only use when you have the UUID.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "transaction_id": { "type": "string", "description": "The transaction UUID to delete" }
                },
                "required": ["transaction_id"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "find_and_delete_transaction",
            "Find a transaction by description/amount/date and delete it directly if exactly one match. Use when user wants to delete without knowing the ID.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "amount": { "type": "number", "description": "Amount to match" },
                    "description": { "type": "string", "description": "Keyword to search" },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd format" },
                    "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"] }
                }
            }
            """))
    ];

    private static List<ChatTool> CategoryTools() =>
    [
        ChatTool.CreateFunctionTool(
            "list_categories",
            "List available categories. Use when user asks about categories or what categories exist.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Filter by type" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_category",
            "Create a new category. Use when user wants to add a new expense/income category.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string", "description": "Category display name" },
                    "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Category type. Default Expense." },
                    "icon": { "type": "string", "description": "Emoji or icon for the category" },
                    "color": { "type": "string", "description": "Color hex code like #FF5733" }
                },
                "required": ["name"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_category",
            "Update a category. Pass the category name — backend resolves by name. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "category_id": { "type": "string", "description": "Category UUID (optional — backend resolves by name)" },
                    "name": { "type": "string", "description": "Current category name to find, OR new name if renaming" },
                    "match_name": { "type": "string", "description": "Current name if 'name' is the new name" },
                    "new_name": { "type": "string", "description": "New display name for the category" },
                    "icon": { "type": "string", "description": "New icon" },
                    "color": { "type": "string", "description": "New color hex" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_category",
            "Delete a category. Pass category name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "category_id": { "type": "string", "description": "Category UUID (optional)" },
                    "name": { "type": "string", "description": "Category name to delete" }
                }
            }
            """))
    ];

    private static List<ChatTool> BudgetTools() =>
    [
        ChatTool.CreateFunctionTool(
            "get_budget",
            "View the budget for a calendar month (year+month). If two pay cycles fall in the same month, prefer get_budget_containing or get_budget_range.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "year": { "type": "integer", "description": "Year (default current year)" },
                    "month": { "type": "integer", "description": "Month 1-12 (default current month)" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_budget_range",
            "View merged budget for an inclusive date range (yyyy-MM-dd). Same as app budget-by-range / custom dashboard budget block.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "start_date": { "type": "string", "description": "Inclusive start yyyy-MM-dd" },
                    "end_date": { "type": "string", "description": "Inclusive end yyyy-MM-dd" }
                },
                "required": ["start_date", "end_date"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_budget_containing",
            "View the single budget whose period contains a given day (e.g. pay cycle mid-month). Use when user refers to salary cycle or a specific date.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "date": { "type": "string", "description": "A calendar day yyyy-MM-dd that must fall inside the budget period" }
                },
                "required": ["date"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_budget",
            "Create a budget for a full calendar month (year+month) or a custom inclusive date range (start_date+end_date, yyyy-MM-dd). Ranges must not overlap existing budgets.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "year": { "type": "integer", "description": "Budget year (default current); used with month when start_date/end_date omitted" },
                    "month": { "type": "integer", "description": "Budget month 1-12 (default current)" },
                    "start_date": { "type": "string", "description": "Inclusive start yyyy-MM-dd (optional; must pair with end_date)" },
                    "end_date": { "type": "string", "description": "Inclusive end yyyy-MM-dd (optional; must pair with start_date)" },
                    "total_amount": { "type": "number", "description": "Total budget amount for the period" }
                },
                "required": ["total_amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_budget",
            "Update a budget total amount. Pass year/month — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "budget_id": { "type": "string", "description": "Budget UUID (optional — backend resolves by year/month)" },
                    "year": { "type": "integer", "description": "Budget year (default current)" },
                    "month": { "type": "integer", "description": "Budget month (default current)" },
                    "total_amount": { "type": "number", "description": "New total budget amount" }
                },
                "required": ["total_amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_budget",
            "Delete a budget. Pass year/month — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "budget_id": { "type": "string", "description": "Budget UUID (optional)" },
                    "year": { "type": "integer", "description": "Budget year (default current)" },
                    "month": { "type": "integer", "description": "Budget month (default current)" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "add_budget_category",
            "Allocate budget to a specific category. Pass category name + year/month — backend resolves everything.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "budget_id": { "type": "string", "description": "Budget UUID (optional — use year/month instead)" },
                    "year": { "type": "integer", "description": "Budget year (default current)" },
                    "month": { "type": "integer", "description": "Budget month (default current)" },
                    "category_id": { "type": "string", "description": "Category UUID (optional — use category name instead)" },
                    "category": { "type": "string", "description": "Category name (e.g. 'Food & Dining'). Backend resolves to ID." },
                    "allocated_amount": { "type": "number", "description": "Amount to allocate for this category" },
                    "alert_threshold": { "type": "number", "description": "Alert at this percentage (default 0.8 = 80%)" }
                },
                "required": ["allocated_amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "remove_budget_category",
            "Remove a category allocation from a budget. Pass category name + year/month — backend resolves.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "budget_category_id": { "type": "string", "description": "Budget category UUID (optional)" },
                    "category": { "type": "string", "description": "Category name to remove from budget" },
                    "year": { "type": "integer", "description": "Budget year (default current)" },
                    "month": { "type": "integer", "description": "Budget month (default current)" }
                }
            }
            """))
    ];

    private static List<ChatTool> RecurringPaymentTools() =>
    [
        ChatTool.CreateFunctionTool(
            "list_recurring_payments",
            "List all recurring payments/subscriptions.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {}
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_recurring_payment",
            "Create a new recurring payment (subscription, bill, scheduled payment).",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string", "description": "Payment name (e.g. Netflix, Rent)" },
                    "amount": { "type": "number", "description": "Payment amount" },
                    "category": { "type": "string", "description": "Category name (e.g. Entertainment, Housing). Backend resolves to ID." },
                    "category_id": { "type": "string", "description": "Category UUID if known (optional)" },
                    "frequency": { "type": "string", "enum": ["Daily", "Weekly", "Monthly", "Yearly"], "description": "Payment frequency. Default Monthly." },
                    "next_due_date": { "type": "string", "description": "Next due date in yyyy-MM-dd. Default today." }
                },
                "required": ["name", "amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_recurring_payment",
            "Update a recurring payment. Pass the payment name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "recurring_id": { "type": "string", "description": "Recurring payment UUID (optional — backend resolves by name)" },
                    "name": { "type": "string", "description": "Payment name to find (e.g. Netflix)" },
                    "match_name": { "type": "string", "description": "Current name if 'name' is the new name" },
                    "amount": { "type": "number", "description": "New amount" },
                    "frequency": { "type": "string", "enum": ["Daily", "Weekly", "Monthly", "Yearly"] },
                    "next_due_date": { "type": "string", "description": "New next due date" },
                    "status": { "type": "string", "enum": ["Active", "Paused", "Completed"] }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_recurring_payment",
            "Delete a recurring payment. Pass the payment name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "recurring_id": { "type": "string", "description": "Recurring payment UUID (optional)" },
                    "name": { "type": "string", "description": "Payment name to delete (e.g. Netflix)" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "mark_recurring_paid",
            "Mark a recurring payment as paid for the current period. Pass name — backend resolves.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "recurring_id": { "type": "string", "description": "Recurring payment UUID (optional)" },
                    "name": { "type": "string", "description": "Payment name to mark as paid (e.g. Netflix)" }
                }
            }
            """))
    ];

    private static List<ChatTool> SavingGoalTools() =>
    [
        ChatTool.CreateFunctionTool(
            "list_saving_goals",
            "List saving goals.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "status": { "type": "string", "enum": ["Active", "Completed", "Cancelled"] }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_saving_goal",
            "Create a new saving goal.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "goal_name": { "type": "string", "description": "Name of the saving goal (e.g. Vacation, Emergency Fund)" },
                    "target_amount": { "type": "number", "description": "Target amount to save" },
                    "target_date": { "type": "string", "description": "Target date in yyyy-MM-dd. Default 6 months from now." },
                    "goal_type": { "type": "string", "enum": ["EmergencyFund", "Vacation", "Vehicle", "Home", "Education", "Retirement", "Other"] },
                    "description": { "type": "string", "description": "Goal description" }
                },
                "required": ["goal_name", "target_amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_saving_goal",
            "Update a saving goal. Pass goal name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "saving_goal_id": { "type": "string", "description": "Saving goal UUID (optional — backend resolves by name)" },
                    "goal_name": { "type": "string", "description": "Goal name to find (e.g. Vacation)" },
                    "name": { "type": "string", "description": "Alias for goal_name" },
                    "new_name": { "type": "string", "description": "New goal name if renaming" },
                    "target_amount": { "type": "number", "description": "New target amount" },
                    "target_date": { "type": "string", "description": "New target date" },
                    "status": { "type": "string", "enum": ["Active", "Completed", "Cancelled"] }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_saving_goal",
            "Delete a saving goal. Pass goal name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "saving_goal_id": { "type": "string", "description": "Saving goal UUID (optional)" },
                    "goal_name": { "type": "string", "description": "Goal name to delete" },
                    "name": { "type": "string", "description": "Alias for goal_name" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "add_saving_contribution",
            "Add deposit/withdrawal to a saving goal. Pass goal name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "saving_goal_id": { "type": "string", "description": "Saving goal UUID (optional)" },
                    "goal_name": { "type": "string", "description": "Goal name (e.g. Vacation). Backend resolves." },
                    "name": { "type": "string", "description": "Alias for goal_name" },
                    "type": { "type": "string", "enum": ["Deposit", "Withdrawal"], "description": "Contribution type. Default Deposit." },
                    "amount": { "type": "number", "description": "Contribution amount" },
                    "date": { "type": "string", "description": "Date in yyyy-MM-dd" },
                    "notes": { "type": "string", "description": "Optional notes" }
                },
                "required": ["amount"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_saving_dashboard",
            "Get saving goals dashboard with overall progress.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {}
            }
            """))
    ];

    private static List<ChatTool> InvestmentTools() =>
    [
        ChatTool.CreateFunctionTool(
            "list_portfolios",
            "List investment portfolios. Use when user asks about their portfolios.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {}
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_portfolio",
            "Create an investment portfolio to organize investments. Use when user says 'add portfolio', 'create portfolio', 'new portfolio'.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string", "description": "Portfolio name (e.g. 'AAPL', 'US Stocks', 'Crypto')" },
                    "description": { "type": "string", "description": "Optional description. Pass empty string if user says 'no description'." }
                },
                "required": ["name"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_portfolio",
            "Update a portfolio. Pass name — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "portfolio_id": { "type": "string", "description": "Portfolio UUID (optional)" },
                    "name": { "type": "string", "description": "Portfolio name to find" },
                    "match_name": { "type": "string", "description": "Current name if 'name' is new name" },
                    "new_name": { "type": "string", "description": "New portfolio name" },
                    "description": { "type": "string", "description": "New description" },
                    "is_active": { "type": "boolean", "description": "Active status" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_portfolio",
            "Delete an investment portfolio. Pass name — backend resolves.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "portfolio_id": { "type": "string", "description": "Portfolio UUID (optional)" },
                    "name": { "type": "string", "description": "Portfolio name to delete" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "list_investments",
            "List investment positions.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "asset_type": { "type": "string", "enum": ["Stock", "Crypto", "Bond", "MutualFund", "RealEstate", "Gold", "Other"] },
                    "status": { "type": "string", "enum": ["Holding", "Sold", "PartialSold"] }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "create_investment_record",
            "Add a new investment position.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "asset_type": { "type": "string", "enum": ["Stock", "Crypto", "Bond", "MutualFund", "RealEstate", "Gold", "Other"] },
                    "asset_name": { "type": "string", "description": "Name of the asset (e.g. Apple Inc, Bitcoin)" },
                    "symbol": { "type": "string", "description": "Ticker symbol (e.g. AAPL, BTC)" },
                    "quantity": { "type": "number", "description": "Number of units purchased" },
                    "purchase_price": { "type": "number", "description": "Price per unit at purchase" },
                    "current_price": { "type": "number", "description": "Current price per unit. Default = purchase_price." },
                    "purchase_date": { "type": "string", "description": "Purchase date yyyy-MM-dd. Default today." },
                    "notes": { "type": "string", "description": "Optional notes" }
                },
                "required": ["asset_name", "quantity", "purchase_price"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "update_investment",
            "Update an investment position. Pass asset name or symbol — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "investment_id": { "type": "string", "description": "Investment UUID (optional — backend resolves by name/symbol)" },
                    "asset_name": { "type": "string", "description": "Asset name to find (e.g. Apple)" },
                    "symbol": { "type": "string", "description": "Symbol to find (e.g. AAPL)" },
                    "current_price": { "type": "number", "description": "Updated current price" },
                    "quantity": { "type": "number", "description": "Updated quantity" },
                    "status": { "type": "string", "enum": ["Holding", "Sold", "PartialSold"] },
                    "notes": { "type": "string", "description": "Updated notes" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "delete_investment",
            "Delete an investment position. Pass asset name or symbol — backend resolves. No UUID needed.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "investment_id": { "type": "string", "description": "Investment UUID (optional)" },
                    "asset_name": { "type": "string", "description": "Asset name to delete" },
                    "symbol": { "type": "string", "description": "Symbol to delete (e.g. AAPL)" }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_investment_dashboard",
            "Get investment dashboard overview.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {}
            }
            """))
    ];

    private static List<ChatTool> ReportTools() =>
    [
        ChatTool.CreateFunctionTool(
            "get_monthly_summary",
            "Get monthly financial summary with income, expense, savings, investment totals.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "month": { "type": "string", "description": "Month in yyyy-MM format. Default current month." }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_yearly_summary",
            "Get yearly financial summary.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "year": { "type": "string", "description": "Year in yyyy format. Default current year." }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_expense_breakdown",
            "Get expense breakdown by category for a month.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "month": { "type": "string", "description": "Month in yyyy-MM format. Default current month." }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_dashboard",
            "Get full dashboard overview for a calendar month (summary, trend, budget, savings, investments, bills).",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "month": { "type": "string", "description": "Month in yyyy-MM format. Default current month." }
                }
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_custom_date_range",
            "Get income/expense/savings/investment totals and expense-by-category breakdown for an inclusive custom date range (matches aggregation custom endpoint).",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "start_date": { "type": "string", "description": "Inclusive start yyyy-MM-dd" },
                    "end_date": { "type": "string", "description": "Inclusive end yyyy-MM-dd" }
                },
                "required": ["start_date", "end_date"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_dashboard_range",
            "Get full dashboard for a custom inclusive date range (same as app custom dashboard). Range must be at most 24 months.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "start_date": { "type": "string", "description": "Inclusive start yyyy-MM-dd" },
                    "end_date": { "type": "string", "description": "Inclusive end yyyy-MM-dd" }
                },
                "required": ["start_date", "end_date"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "suggest_reports_download",
            "When the user wants to download Excel, export a spreadsheet, or download a report file: call this ONLY. Does NOT call any server export API — returns a clientAction so the app can show the Reports download button.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "start_month": { "type": "string", "description": "Start month yyyy-MM for the export range. Default current month." },
                    "end_month": { "type": "string", "description": "End month yyyy-MM. Default same as start_month." }
                }
            }
            """))
    ];
}
