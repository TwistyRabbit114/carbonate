using Carbonate.Application.Features.Boards;
using Carbonate.Application.Features.Venues;
using Carbonate.Application.Platform.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<PasswordPolicy>();

        //boards and cards (B, FR-19 to FR-21)
        services.AddScoped<IBoardService, BoardService>();

        //venues and site visits (B, FR-32, FR-33)
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<ISiteVisitService, SiteVisitService>();

        return services;
    }
}
