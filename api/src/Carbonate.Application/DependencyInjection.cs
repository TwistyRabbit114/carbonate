using Carbonate.Application.Features.Boards;
using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Features.Venues;
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

        // Stock, requirements and order lists (D, FR-24-30)
        services.AddScoped<IStockCatalogueService, StockCatalogueService>();
        services.AddScoped<IStockRequirementService, StockRequirementService>();
        services.AddScoped<IOrderListService, OrderListService>();

        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<PasswordPolicy>();

        //boards and cards (B, FR-19 to FR-21)
        services.AddScoped<IBoardService, BoardService>();

        //venues and site visits (B, FR-32, FR-33)
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<ISiteVisitService, SiteVisitService>();

        return services;
    }
}
