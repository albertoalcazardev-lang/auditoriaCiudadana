using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using VyTSystems.API.Extensions;
using VyTSystems.API.Middleware;
using VyTSystems.API.Services.Authentication;
using VyTSystems.Application;
using VyTSystems.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// El filtro MVC ValidateAntiForgeryToken se registra con los servicios de MVC ViewFeatures.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressReadingTokenFromFormBody = true;
    options.Cookie.Name = "__Host-VyTSystems.Csrf";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (string.Equals(context.Description.RelativePath, "api/solicitudes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters ??= [];
            var existente = operation.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase) &&
                p.In == ParameterLocation.Header);

            if (existente is OpenApiParameter parametro)
            {
                parametro.Required = true;
                parametro.Description = "Clave única de idempotencia generada por el cliente (^[A-Za-z0-9._:-]{1,100}$).";
            }
            else if (existente is null)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "Idempotency-Key",
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "Clave única de idempotencia generada por el cliente (^[A-Za-z0-9._:-]{1,100}$).",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        MaxLength = 100,
                        Pattern = "^[A-Za-z0-9._:-]{1,100}$"
                    }
                });
            }
        }

        if (string.Equals(context.Description.RelativePath, "api/solicitudes/{id}", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(context.Description.HttpMethod, "PUT", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters ??= [];
            var existente = operation.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, "If-Match", StringComparison.OrdinalIgnoreCase) &&
                p.In == ParameterLocation.Header);

            if (existente is OpenApiParameter parametro)
            {
                parametro.Required = true;
                parametro.Description = "ETag fuerte vigente de la solicitud entre comillas dobles (RFC 9110).";
            }
            else if (existente is null)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "If-Match",
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "ETag fuerte vigente de la solicitud entre comillas dobles (RFC 9110).",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Pattern = "^\"[\\x21\\x23-\\x7E]{1,128}\"$"
                    }
                });
            }
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddConfiguredCors(builder.Configuration);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-VyTSystems.Auth";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
        EscribirProblemaAuthAsync(
            context,
            StatusCodes.Status401Unauthorized,
            "No autenticado",
            "Debes iniciar sesión para acceder a este recurso.",
            "Auth.Unauthenticated",
            () => context.Response.Redirect(context.RedirectUri));
    options.Events.OnRedirectToAccessDenied = context =>
        EscribirProblemaAuthAsync(
            context,
            StatusCodes.Status403Forbidden,
            "Acceso denegado",
            "No cuentas con permisos para realizar esta acción.",
            "Auth.Forbidden",
            () => context.Response.Redirect(context.RedirectUri));
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthenticatedSessionService, AuthenticatedSessionService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsExtensions.FrontendPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static Task EscribirProblemaAuthAsync(
    RedirectContext<CookieAuthenticationOptions> context,
    int statusCode,
    string title,
    string detail,
    string code,
    Action fallbackRedirect)
{
    if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        fallbackRedirect();
        return Task.CompletedTask;
    }

    context.Response.StatusCode = statusCode;
    context.Response.Headers.CacheControl = "no-cache, no-store";

    var problem = new ProblemDetails
    {
        Status = statusCode,
        Title = title,
        Detail = detail,
        Instance = context.Request.Path
    };
    problem.Extensions["code"] = code;

    return Results.Problem(problem).ExecuteAsync(context.HttpContext);
}

public partial class Program;
