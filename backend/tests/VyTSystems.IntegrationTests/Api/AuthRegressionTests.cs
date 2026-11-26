using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Api;

[Collection(MySqlIntegrationCollection.Name)]
public sealed class AuthRegressionTests(MySqlTestFixture fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FlujoCompletoAuth_CsrfLoginMeYLogout_FuncionaParaAdministradorYConsulta(bool esAdministrador)
    {
        await using var factory = fixture.CreateWebApplicationFactory();
        using var client = factory.CreateHttpsClientWithCookies();

        // 1. CSRF
        var csrfRes = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfRes.StatusCode);
        Assert.Contains("no-store", csrfRes.Headers.CacheControl?.ToString());
        var csrfJson = await csrfRes.Content.ReadFromJsonAsync<JsonElement>();
        var csrfToken = csrfJson.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(csrfToken));

        // 2. Login inválido
        using (var invalidReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                identifier = fixture.AdminUserName,
                password = $"Wrong_{Guid.NewGuid():N}!",
                requestedAccess = "Administrador",
                rememberSession = false
            })
        })
        {
            invalidReq.Headers.Add("X-CSRF-TOKEN", csrfToken);
            var invalidRes = await client.SendAsync(invalidReq);
            Assert.Equal(HttpStatusCode.Unauthorized, invalidRes.StatusCode);
        }

        // 3. Login válido (Administrador o Consulta)
        var user = esAdministrador ? fixture.AdminUserName : fixture.ConsultaUserName;
        var pwd = esAdministrador ? fixture.AdminPassword : fixture.ConsultaPassword;
        var access = esAdministrador ? "Administrador" : "Consulta";

        await AuthTestClientHelper.IniciarSesionAsync(client, user, pwd, access);

        // 4. Me
        var meRes = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var meJson = await meRes.Content.ReadFromJsonAsync<JsonElement>();
        var roles = meJson.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(access, roles);

        // 5. Logout (requiere CSRF fresco tras login)
        var postLoginCsrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(client);
        using (var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
        {
            logoutReq.Headers.Add("X-CSRF-TOKEN", postLoginCsrf);
            var logoutRes = await client.SendAsync(logoutReq);
            Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);
        }

        // 6. Me tras logout -> 401 sin redirección 302
        var meAfterLogout = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
    }
}
