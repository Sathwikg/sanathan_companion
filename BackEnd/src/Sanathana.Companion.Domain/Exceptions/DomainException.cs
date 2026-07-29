namespace Sanathana.Companion.Domain.Exceptions;

/// <summary>Base type for business-rule violations. Mapped to HTTP 400 by the API unless a more specific type applies.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
