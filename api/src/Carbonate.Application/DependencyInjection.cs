using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFinancialMasker, FinancialMasker>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<ICommercialService, CommercialService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<PasswordPolicy>();

        return services;
    }
}
