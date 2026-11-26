using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VyTSystems.IntegrationTests.Fixtures;

/// <summary>
/// Fábrica de pruebas HTTP sobre la aplicación real conectada al contenedor MySQL 8.4 aislado.
/// </summary>
public sealed class VyTSystemsWebAppFactory(
    string connectionString,
    string environment = "Production",
    TimeProvider? customTimeProvider = null,
    Action<IServiceCollection>? configureTestServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            });
        });

        if (customTimeProvider is not null || configureTestServices is not null)
        {
            builder.ConfigureServices(services =>
            {
                if (customTimeProvider is not null)
                {
                    services.Replace(ServiceDescriptor.Singleton(customTimeProvider));
                }

                configureTestServices?.Invoke(services);
            });
        }
    }

    public HttpClient CreateHttpsClientWithCookies()
    {
        var handler = new CookieContainerHandler();
        return CreateDefaultClient(new Uri("https://localhost"), handler);
    }

    private sealed class CookieContainerHandler : DelegatingHandler
    {
        private readonly CookieContainer _cookies = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                var cookieHeader = _cookies.GetCookieHeader(request.RequestUri);
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    request.Headers.Remove("Cookie");
                    request.Headers.Add("Cookie", cookieHeader);
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (request.RequestUri is not null &&
                response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    _cookies.SetCookies(request.RequestUri, setCookie);
                }
            }

            return response;
        }
    }
}
