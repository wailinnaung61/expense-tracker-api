/*using expense_tracker_backend.Domain.Shared.Constants;
using System.Text.Json.Serialization;
namespace expense_tracker_backend.Application.DTOs;

public record Tranaction(
    Guid TranactionId,
    Guid UserId,
    AppConstants.TransactionType type,
    string CategoryId,
    string CategoryName,
    decimal Amount,
    string Description,
    string Merchant,
    string PaymentMethod,
    AppConstants.PaymentStatus status,
    string TranactionDate,
    string ImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Note
);

public record CreateTranactionDto(
    AppConstants.TransactionType type,
    string CategoryId,
    decimal Amount,
    string TranactionDate,
    AppConstants.PaymentStatus status,
    string Description,
    string Note,
    string ImageUrl
);

public record UpdateTranactionDto(
    AppConstants.TransactionType type,
    string CategoryId,
    decimal Amount,
    string TranactionDate,
    AppConstants.PaymentStatus status,
    string Description,
    string Note,
    string ImageUrl
);
*/