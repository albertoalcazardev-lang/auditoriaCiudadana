namespace VyTSystems.API.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicyName = "VyTFrontendPolicy";

    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendPolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        .AllowAnyHeader()
                        .WithExposedHeaders("ETag");
                }
            });
        });

        return services;
    }
}
