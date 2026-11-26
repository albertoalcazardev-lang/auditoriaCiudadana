using Microsoft.AspNetCore.Mvc.Filters;

namespace VyTSystems.API.Filters;

// Incluye los 400 que MVC devuelve antes de la acción (CSRF o JSON inválido).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class NoStoreAttribute : Attribute, IAsyncAlwaysRunResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-cache, no-store";
        return next();
    }
}
