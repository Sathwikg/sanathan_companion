namespace Sanathana.Companion.Domain.Exceptions;

/// <summary>Thrown when a request conflicts with existing state (e.g. duplicate email). Mapped to HTTP 409.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
