using System.Diagnostics;
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

        // Same id ASP.NET Core writes as "traceId" in the ProblemDetails body.
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception (trace {TraceId})", traceId);
        else
            // "Meu pedido sumiu": often the order was refused (out of stock, unavailable) and never existed.
            // The trace id is also in the response body, so the error the attendant saw leads to this line.
            logger.LogWarning("{Method} {Path} by {User} refused with {Status}: {Reason} (trace {TraceId})",
                context.Request.Method, context.Request.Path, context.User.Identity?.Name ?? "anônimo",
                status, exception.Message, traceId);

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
