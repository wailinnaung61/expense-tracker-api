using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class InvestmentChatHandler
{
    private readonly IInvestmentService _investmentService;
    private readonly IInvestmentPortfolioService _portfolioService;

    public InvestmentChatHandler(
        IInvestmentService investmentService,
        IInvestmentPortfolioService portfolioService)
    {
        _investmentService = investmentService;
        _portfolioService = portfolioService;
    }

    // ── Portfolio CRUD ────────────────────────────────────────────────────────

    public async Task<(string, object?)> ListPortfoliosAsync(Guid userId)
    {
        var result = await _portfolioService.GetAllAsync(userId);

        if (result.Count == 0)
            return ("No portfolios found. Create one to organize your investments!", result);

        var lines = result.Select(p =>
            $"• {p.Name}{(string.IsNullOrWhiteSpace(p.Description) ? "" : $" — {p.Description}")} [{(p.IsActive ? "Active" : "Inactive")}]");
        var summary = $"Portfolios:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> CreatePortfolioAsync(Guid userId, JsonElement args)
    {
        var name = TryStr(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            return ("Please provide a portfolio name (e.g. 'US Stocks', 'Crypto').", null);

        var description = TryStr(args, "description") ?? "";

        var request = new CreateInvestmentPortfolioRequest(name, description);
        var result = await _portfolioService.CreateAsync(userId, request);

        return ($"Created portfolio: {result.Name}", result);
    }

    public async Task<(string, object?)> UpdatePortfolioAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolvePortfolioAsync(userId, args);
        if (existing is null)
            return ("Portfolio not found. Try 'list portfolios' to see your portfolios.", null);

        var name = TryStr(args, "new_name") ?? TryStr(args, "name") ?? existing.Name;
        var description = TryStr(args, "description") ?? existing.Description;
        var isActive = existing.IsActive;
        if (args.TryGetProperty("is_active", out var ia))
        {
            if (ia.ValueKind == JsonValueKind.True) isActive = true;
            else if (ia.ValueKind == JsonValueKind.False) isActive = false;
        }

        var request = new UpdateInvestmentPortfolioRequest(name, description, isActive);
        var result = await _portfolioService.UpdateAsync(userId, existing.PortfolioId, request);

        return result is not null
            ? ($"Updated portfolio: {result.Name}", result)
            : ("Failed to update portfolio.", null);
    }

    public async Task<(string, object?)> DeletePortfolioAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolvePortfolioAsync(userId, args);
        if (existing is null)
            return ("Portfolio not found. Try 'list portfolios' to see your portfolios.", null);

        var result = await _portfolioService.DeleteAsync(userId, existing.PortfolioId);
        return result
            ? ($"Deleted portfolio: {existing.Name}", true)
            : ("Failed to delete portfolio.", false);
    }

    private async Task<InvestmentPortfolioDto?> ResolvePortfolioAsync(Guid userId, JsonElement args)
    {
        var idStr = TryStr(args, "portfolio_id");
        if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var portfolioId))
            return await _portfolioService.GetByIdAsync(userId, portfolioId);

        var name = TryStr(args, "name") ?? TryStr(args, "match_name") ?? TryStr(args, "portfolio_name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var all = await _portfolioService.GetAllAsync(userId);
        return all.FirstOrDefault(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    // ── Investment Position CRUD ──────────────────────────────────────────────

    public async Task<(string, object?)> ListInvestmentsAsync(Guid userId, JsonElement args)
    {
        AppConstants.AssetType? assetType = null;
        if (args.TryGetProperty("asset_type", out var at) && Enum.TryParse<AppConstants.AssetType>(at.GetString(), true, out var parsedType))
            assetType = parsedType;

        AppConstants.InvestmentStatus? status = null;
        if (args.TryGetProperty("status", out var s) && Enum.TryParse<AppConstants.InvestmentStatus>(s.GetString(), true, out var parsedStatus))
            status = parsedStatus;

        var filter = new InvestmentFilterRequest { AssetType = assetType, Status = status, PageSize = 20 };
        var result = await _investmentService.GetInvestmentsAsync(userId, filter);

        if (result.Items.Count == 0)
            return ("No investments found.", result);

        var lines = result.Items.Select(inv =>
            $"• {inv.AssetName} ({inv.Symbol}): {inv.Quantity} x {inv.CurrentPrice:N2} = {inv.CurrentValue:N0} [{inv.Status}] P/L: {inv.ProfitLoss:N0}");
        var summary = $"Investments:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> CreateAsync(Guid userId, JsonElement args)
    {
        var assetName = TryStr(args, "asset_name");
        if (string.IsNullOrWhiteSpace(assetName))
            return ("Please provide the asset name (e.g. Apple, Bitcoin).", null);

        var assetTypeStr = TryStr(args, "asset_type") ?? "Stock";
        if (!Enum.TryParse<AppConstants.AssetType>(assetTypeStr, true, out var assetType))
            assetType = AppConstants.AssetType.Stock;

        var symbol = TryStr(args, "symbol") ?? "";
        var quantity = TryDecimal(args, "quantity");
        if (quantity <= 0)
            return ("Please provide the quantity (number of units).", null);

        var purchasePrice = TryDecimal(args, "purchase_price");
        if (purchasePrice <= 0)
            return ("Please provide the purchase price per unit.", null);

        var currentPrice = TryDecimal(args, "current_price");
        if (currentPrice <= 0) currentPrice = purchasePrice;

        var purchaseDate = TryStr(args, "purchase_date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var notes = TryStr(args, "notes") ?? "";

        Guid? portfolioId = null;
        var portfolioName = TryStr(args, "portfolio");
        if (!string.IsNullOrWhiteSpace(portfolioName))
        {
            var all = await _portfolioService.GetAllAsync(userId);
            var match = all.FirstOrDefault(p => p.Name.Contains(portfolioName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) portfolioId = match.PortfolioId;
        }

        var request = new CreateInvestmentRequest(portfolioId, assetType, assetName, symbol, quantity, purchasePrice, currentPrice, purchaseDate, notes);
        var result = await _investmentService.CreateAsync(userId, request);

        return ($"Added investment: {result.AssetName} ({result.Symbol}) — {result.Quantity} units at {result.PurchasePrice:N2}", result);
    }

    public async Task<(string, object?)> UpdateAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveInvestmentAsync(userId, args);
        if (existing is null)
            return ("Investment not found. Try 'list investments' to see your positions.", null);

        var currentPrice = TryDecimal(args, "current_price");
        if (currentPrice <= 0) currentPrice = existing.CurrentPrice;

        var quantity = TryDecimal(args, "quantity");
        if (quantity <= 0) quantity = existing.Quantity;

        var notes = TryStr(args, "notes") ?? existing.Notes;

        var status = AppConstants.InvestmentStatus.Holding;
        if (args.TryGetProperty("status", out var s) && Enum.TryParse<AppConstants.InvestmentStatus>(s.GetString(), true, out var parsedStatus))
            status = parsedStatus;
        else if (Enum.TryParse<AppConstants.InvestmentStatus>(existing.Status, true, out var existingStatus))
            status = existingStatus;

        var request = new UpdateInvestmentRequest(
            existing.PortfolioId, existing.AssetName, existing.Symbol,
            quantity, existing.PurchasePrice, currentPrice,
            existing.PurchaseDate, status, notes);
        var result = await _investmentService.UpdateAsync(userId, existing.InvestmentId, request);

        return result is not null
            ? ($"Updated investment: {result.AssetName} — Price: {result.CurrentPrice:N2}, P/L: {result.ProfitLoss:N0}", result)
            : ("Failed to update investment.", null);
    }

    public async Task<(string, object?)> DeleteAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveInvestmentAsync(userId, args);
        if (existing is null)
            return ("Investment not found. Try 'list investments' to see your positions.", null);

        var result = await _investmentService.DeleteAsync(userId, existing.InvestmentId);
        return result
            ? ($"Deleted investment: {existing.AssetName}", true)
            : ("Investment not found.", false);
    }

    public async Task<(string, object?)> GetDashboardAsync(Guid userId)
    {
        var result = await _investmentService.GetDashboardAsync(userId);

        var summary = $"Investment Dashboard:\n" +
            $"Total Invested: {result.TotalInvested:N0} | Current Value: {result.CurrentValue:N0}\n" +
            $"P/L: {result.TotalProfitLoss:N0} ({result.ReturnPercentage:F1}%)";

        if (result.AssetAllocation.Count > 0)
        {
            var lines = result.AssetAllocation.Select(a =>
                $"• {a.AssetType}: {a.CurrentValue:N0} ({a.Percentage:F1}%)");
            summary += $"\n\nAllocation:\n{string.Join("\n", lines)}";
        }

        return (summary, result);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private async Task<InvestmentDto?> ResolveInvestmentAsync(Guid userId, JsonElement args)
    {
        var idStr = TryStr(args, "investment_id");
        if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var investmentId))
            return await _investmentService.GetByIdAsync(userId, investmentId);

        var name = TryStr(args, "asset_name") ?? TryStr(args, "name") ?? TryStr(args, "match_name");
        var symbol = TryStr(args, "symbol");
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(symbol)) return null;

        var keyword = !string.IsNullOrWhiteSpace(name) ? name : symbol;
        var filter = new InvestmentFilterRequest { Keyword = keyword, PageSize = 5 };
        var result = await _investmentService.GetInvestmentsAsync(userId, filter);

        if (result.Items.Count == 1)
            return result.Items[0];

        if (!string.IsNullOrWhiteSpace(symbol) && result.Items.Count > 1)
        {
            var exactSymbol = result.Items.FirstOrDefault(i =>
                i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (exactSymbol is not null) return exactSymbol;
        }

        return result.Items.FirstOrDefault();
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static decimal TryDecimal(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0;
}
