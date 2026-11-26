using Microsoft.AspNetCore.Identity;
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
builder.Services.AddOpenApi();
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
