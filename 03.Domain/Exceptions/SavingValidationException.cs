namespace expense_tracker_backend.Domain.Exceptions;

public class SavingValidationException : Exception
{
    public string ResourceKey { get; }

    public SavingValidationException(string resourceKey) : base(resourceKey)
    {
        ResourceKey = resourceKey;
    }

    public SavingValidationException(string resourceKey, Exception innerException)
        : base(resourceKey, innerException)
    {
        ResourceKey = resourceKey;
    }
}
