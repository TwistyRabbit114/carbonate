using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFinancialMasker, FinancialMasker>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<PasswordPolicy>();

        return services;
    }
}
