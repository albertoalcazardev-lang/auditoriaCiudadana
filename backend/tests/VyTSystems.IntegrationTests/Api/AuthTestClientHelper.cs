using System.Net.Http.Json;
using System.Text.Json;

namespace VyTSystems.IntegrationTests.Api;

internal static class AuthTestClientHelper
{
    public static async Task<string> ObtenerCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("requestToken").GetString()!;
    }

    public static async Task IniciarSesionAsync(
        HttpClient client,
        string identifier,
        string password,
        string requestedAccess)
    {
        var csrf = await ObtenerCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                identifier,
                password,
                requestedAccess,
                rememberSession = false
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
