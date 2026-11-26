using System.Data.Common;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using VyTSystems.Domain.Common;

namespace VyTSystems.API.Middleware;

/// <summary>
/// Manejador global de excepciones bajo formato estándar ProblemDetails (RFC 7807).
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-cache, no-store";

        ProblemDetails problem;
        if (exception is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
            problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Revisa los campos indicados."
            };
        }
        else if (exception is DomainException domainException)
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Regla de negocio incumplida",
                Detail = domainException.Message
            };
        }
        else if (EsErrorDeDisponibilidad(exception))
        {
            var cause = exception.GetBaseException();
            var codigo = (cause as MySqlException)?.Number ?? (cause as DbException)?.ErrorCode;
            logger.LogError(
                "Indisponibilidad de datos en {Method} {Path}: {Tipo}, código {Codigo}.",
                httpContext.Request.Method,
                httpContext.Request.Path,
                cause.GetType().Name,
                codigo);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Servicio no disponible",
                Detail = "No fue posible completar la operación por indisponibilidad temporal del servicio de datos. Inténtalo de nuevo."
            };
            problem.Extensions["code"] = "Persistence.Unavailable";
        }
        else
        {
            logger.LogError(exception, "Excepción no controlada en {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = "Ocurrió un error inesperado al procesar la solicitud."
            };
            problem.Extensions["code"] = "Server.UnexpectedError";
        }

        problem.Instance = httpContext.Request.Path;
        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }

    private static bool EsErrorDeDisponibilidad(Exception exception)
    {
        if (exception is TimeoutException)
        {
            return true;
        }

        if (exception is DbException dbEx)
        {
            return !EsSqlStateIntegridad(dbEx);
        }

        if (exception is DbUpdateException { InnerException: DbException innerDb })
        {
            return !EsSqlStateIntegridad(innerDb);
        }

        var baseEx = exception.GetBaseException();
        return baseEx is TimeoutException || (baseEx is DbException baseDb && !EsSqlStateIntegridad(baseDb));
    }

    private static bool EsSqlStateIntegridad(DbException dbException) =>
        dbException.SqlState is { Length: >= 2 } state &&
        state.StartsWith("23", StringComparison.Ordinal);
}
