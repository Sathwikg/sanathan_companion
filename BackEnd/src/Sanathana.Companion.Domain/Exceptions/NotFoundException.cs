namespace Sanathana.Companion.Domain.Exceptions;

/// <summary>Thrown when a required entity cannot be found. Mapped to HTTP 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}
