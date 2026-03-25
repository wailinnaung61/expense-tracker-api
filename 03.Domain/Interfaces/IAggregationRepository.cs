using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IAggregationRepository
{
    Task UpdateAggregationsAsync(Transaction transaction);
}
