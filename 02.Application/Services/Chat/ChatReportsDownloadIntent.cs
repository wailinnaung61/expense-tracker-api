using System.Text.Json;
using System.Text.RegularExpressions;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat;

/// <summary>
/// Detects user intent to download Excel / export reports and builds a <see cref="ChatClientAction"/>
/// for the frontend (no server export API from chat).
/// </summary>
public static class ChatReportsDownloadIntent
{
    private static readonly Regex IntentPattern = new(
        @"\b(download|export|get)\b.{0,40}\b(excel|xlsx|spreadsheet)\b|\b(download|export)\s+report\b|\bexport\s+data\b|\bdownload\s+data\b|\bexport\s+this\s+month\b|\bdownload\s+this\s+month\b|\bdownload\s+my\s+report\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IsoMonthPattern = new(
        @"\b(20[0-9]{2}-(?:0[1-9]|1[0-2]))\b",
        RegexOptions.Compiled);

    /// <summary>yyyy-M, yyyy/MM, yyyy-M-d (year+month used for export range).</summary>
    private static readonly Regex LooseYearMonthPattern = new(
        @"\b(20[0-9]{2})\s*[-/]\s*(0?[1-9]|1[0-2])(?:\s*[-/]\s*\d{1,2})?\b",
        RegexOptions.Compiled);

    public static bool Matches(string message) => IntentPattern.IsMatch(message.Trim());

    public static ChatClientAction BuildClientActionFromMessage(string message)
    {
        var m = message.Trim();
        var now = DateTime.UtcNow;

        if (Regex.IsMatch(m, @"\blast\s+month\b", RegexOptions.IgnoreCase))
        {
            var d = now.AddMonths(-1);
            var month = d.ToString("yyyy-MM");
            return new ChatClientAction(AppConstants.ChatClientActionType.ShowReportsDownload, month, month);
        }

        var months = CollectYyyyMmTokens(m);
        if (months.Count >= 2)
            return new ChatClientAction(AppConstants.ChatClientActionType.ShowReportsDownload, months[0], months[1]);
        if (months.Count == 1)
            return new ChatClientAction(AppConstants.ChatClientActionType.ShowReportsDownload, months[0], months[0]);

        var current = now.ToString("yyyy-MM");
        return new ChatClientAction(AppConstants.ChatClientActionType.ShowReportsDownload, current, current);
    }

    /// <summary>Distinct yyyy-MM values in left-to-right order (for export range).</summary>
    private static List<string> CollectYyyyMmTokens(string m)
    {
        var found = new List<string>();

        void AddIfValid(string yyyyMm)
        {
            if (found.Count == 0 || !string.Equals(found[^1], yyyyMm, StringComparison.Ordinal))
                found.Add(yyyyMm);
        }

        foreach (Match x in IsoMonthPattern.Matches(m))
            AddIfValid(x.Value);

        foreach (Match x in LooseYearMonthPattern.Matches(m))
        {
            if (!int.TryParse(x.Groups[1].Value, out var y) || !int.TryParse(x.Groups[2].Value, out var mo))
                continue;
            if (mo is < 1 or > 12) continue;
            AddIfValid($"{y:D4}-{mo:D2}");
        }

        return found;
    }

    public static ChatClientAction BuildClientActionFromToolArgs(JsonElement args)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM");
        var start = TryStr(args, "start_month") ?? now;
        var end = TryStr(args, "end_month") ?? start;
        return new ChatClientAction(AppConstants.ChatClientActionType.ShowReportsDownload, start, end);
    }

    public const string AssistantMessage =
        "Open **Reports** in the app and tap **Download** (Excel) for that period. I can’t start the file from chat — use the button below if your app shows it.";

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;
}
