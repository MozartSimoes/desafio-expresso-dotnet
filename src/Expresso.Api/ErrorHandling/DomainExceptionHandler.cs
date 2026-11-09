using Expresso.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.ErrorHandling;

/// <summary>
/// Turns exceptions into RFC 7807 ProblemDetails, so controllers need no try/catch.
/// Unexpected errors are logged and answered with a generic 500 that leaks no internals.
/// </summary>
public class DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito"),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception switch
                {
                    DomainException => exception.Message,
                    DbUpdateConcurrencyException => "O registro foi alterado por outra pessoa. Atualize e tente de novo.",
                    _ => null
                }
            }
        });
    }
}
