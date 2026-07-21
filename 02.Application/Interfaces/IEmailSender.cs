namespace expense_tracker_backend.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string bodyHtml, CancellationToken ct = default);
}
