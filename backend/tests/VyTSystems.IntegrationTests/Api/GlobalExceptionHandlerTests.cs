using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VyTSystems.API.Middleware;

namespace VyTSystems.IntegrationTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    private sealed class FakeDbException(string message, string? sqlState) : DbException(message)
    {
        public override string? SqlState { get; } = sqlState;
    }

    private static (DefaultHttpContext Context, MemoryStream Body, ServiceProvider Services) CreateHttpContext(
        string method,
        string path)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Method = method;
        context.Request.Path = path;
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        return (context, responseStream, provider);
    }

    [Fact]
    public async Task TryHandleAsync_DbExceptionSqlState08S01_Devuelve503PersistenceUnavailable()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var (context, responseStream, services) = CreateHttpContext("GET", "/api/solicitudes/catalogos");
        await using var _ = services;

        var handled = await handler.TryHandleAsync(
            context,
            new FakeDbException("Server=secret;Pwd=secret;", "08S01"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());

        var bodyText = Encoding.UTF8.GetString(responseStream.ToArray());
        Assert.DoesNotContain("secret", bodyText, StringComparison.OrdinalIgnoreCase);
        var json = JsonDocument.Parse(bodyText).RootElement;
        Assert.Equal("Persistence.Unavailable", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_DbExceptionSqlState23000_ExcluidaDe503Devuelve500()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var (context, responseStream, services) = CreateHttpContext("POST", "/api/solicitudes");
        await using var _ = services;

        var handled = await handler.TryHandleAsync(
            context,
            new FakeDbException("Duplicate constraint", "23000"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());

        var bodyText = Encoding.UTF8.GetString(responseStream.ToArray());
        var json = JsonDocument.Parse(bodyText).RootElement;
        Assert.Equal("Server.UnexpectedError", json.GetProperty("code").GetString());
    }
}
