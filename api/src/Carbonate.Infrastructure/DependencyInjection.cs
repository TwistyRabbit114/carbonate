using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Infrastructure.Persistence;
using Carbonate.Infrastructure.Platform.Audit;
using Carbonate.Infrastructure.Platform.Auth;
using Carbonate.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CemDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Sql")));

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddHttpClient<IPwnedPasswordChecker, PwnedPasswordChecker>(client =>
        {
            client.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddScoped<PlatformSeeder>();
        services.AddHostedService<PlatformSeedHostedService>();

        return services;
    }
}
