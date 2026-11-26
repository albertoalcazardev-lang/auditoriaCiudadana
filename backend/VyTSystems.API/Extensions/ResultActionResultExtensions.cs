using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.API.Extensions;

internal static class ResultActionResultExtensions
{
    public static IActionResult UnauthorizedProblem(this ControllerBase controller) =>
        BuildProblemResult(
            controller,
            StatusCodes.Status401Unauthorized,
            "No autenticado",
            "Debes iniciar sesión para acceder a este recurso.",
            "Auth.Unauthenticated");

    public static IActionResult ToActionResult<TSource, TResponse>(
        this ControllerBase controller,
        Result<TSource> result,
        Func<TSource, TResponse> map) =>
        result.IsFailure
            ? controller.ToFailureResult(result)
            : controller.Ok(map(result.Value));

    public static IActionResult ToActionResultWithETag<TSource, TResponse>(
        this ControllerBase controller,
        Result<TSource> result,
        Func<TSource, TResponse> map,
        Func<TSource, string> formatETag)
    {
        if (result.IsFailure)
        {
            return controller.ToFailureResult(result);
        }

        controller.Response.Headers.ETag = formatETag(result.Value);
        return controller.Ok(map(result.Value));
    }

    public static IActionResult ToCreatedOrOkResult<TSource, TResponse>(
        this ControllerBase controller,
        Result<TSource> result,
        Func<TSource, TResponse> map,
        Func<TSource, bool> isIdempotentReplay)
    {
        if (result.IsFailure)
        {
            return controller.ToFailureResult(result);
        }

        var response = map(result.Value);
        return isIdempotentReplay(result.Value)
            ? controller.Ok(response)
            : controller.StatusCode(StatusCodes.Status201Created, response);
    }

    public static IActionResult ToFailureResult(this ControllerBase controller, Result result)
    {
        if (result.Error.Type == ErrorType.Validation)
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in result.ValidationErrors)
            {
                var propertyName = JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName);
                modelState.AddModelError(propertyName, error.ErrorMessage);
            }

            var validationProblem = controller.ProblemDetailsFactory.CreateValidationProblemDetails(
                controller.HttpContext,
                modelState,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Revisa los campos indicados.",
                instance: controller.HttpContext.Request.Path);

            return controller.ValidationProblem(validationProblem);
        }

        if (result.Error.Type == ErrorType.Unauthorized)
        {
            return BuildProblemResult(
                controller,
                StatusCodes.Status401Unauthorized,
                "No autenticado",
                result.Error.Description,
                result.Error.Code);
        }

        if (result.Error.Type == ErrorType.Conflict)
        {
            return BuildProblemResult(
                controller,
                StatusCodes.Status409Conflict,
                "Conflicto en la operación",
                result.Error.Description,
                result.Error.Code);
        }

        if (result.Error.Type == ErrorType.NotFound)
        {
            return BuildProblemResult(
                controller,
                StatusCodes.Status404NotFound,
                "Recurso no encontrado",
                result.Error.Description,
                result.Error.Code);
        }

        if (result.Error.Type == ErrorType.Failure &&
            string.Equals(result.Error.Code, "Persistence.Unavailable", StringComparison.Ordinal))
        {
            return BuildProblemResult(
                controller,
                StatusCodes.Status503ServiceUnavailable,
                "Servicio no disponible",
                result.Error.Description,
                result.Error.Code);
        }

        return BuildProblemResult(
            controller,
            StatusCodes.Status500InternalServerError,
            "Error interno del servidor",
            "Ocurrió un error inesperado al procesar la solicitud.",
            "Server.UnexpectedError");
    }

    private static ObjectResult BuildProblemResult(
        ControllerBase controller,
        int statusCode,
        string title,
        string detail,
        string code)
    {
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: controller.HttpContext.Request.Path);
        problem.Extensions["code"] = code;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
