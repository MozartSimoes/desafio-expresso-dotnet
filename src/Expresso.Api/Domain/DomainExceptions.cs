namespace Expresso.Api.Domain;

/// <summary>Base for errors the client can fix; the global handler turns each type into an HTTP status.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>404: the resource does not exist.</summary>
public sealed class NotFoundException(string entity, object key)
    : DomainException($"{entity} {key} não encontrado(a).");

/// <summary>409: the request conflicts with the current state (duplicate, invalid status transition).</summary>
public sealed class ConflictException(string message) : DomainException(message);

/// <summary>422: the input is well formed but breaks a business rule.</summary>
public sealed class BusinessRuleException(string message) : DomainException(message);
