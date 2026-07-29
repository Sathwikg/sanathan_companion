namespace Sanathana.Companion.Domain.Exceptions;

/// <summary>Thrown for invalid input that isn't a simple field validation. Mapped to HTTP 400.</summary>
public sealed class BadRequestException : DomainException
{
    public BadRequestException(string message) : base(message) { }
}
