using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
        }

        problem.Instance = httpContext.Request.Path;
        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
