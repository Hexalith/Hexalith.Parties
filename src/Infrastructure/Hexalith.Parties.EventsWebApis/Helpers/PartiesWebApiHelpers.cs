namespace Hexalith.Parties.EventsWebApis.Helpers;

using Hexalith.Application.Projections;
using Hexalith.Domain.Events;
using Hexalith.Infrastructure.DaprRuntime.Helpers;
using Hexalith.Infrastructure.WebApis.PartiesEvents.Projections;
using Hexalith.Parties.Domain.Aggregates;
using Hexalith.Parties.Events;
using Hexalith.Parties.EventsWebApis.Controllers;
using Hexalith.Parties.EventsWebApis.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Class PartiesWebApiHelpers.
/// </summary>
public static class PartiesWebApiHelpers
{
    /// <summary>
    /// Adds the customer projections.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="applicationId">Name of the application.</param>
    /// <returns>IServiceCollection.</returns>
    /// <exception cref="ArgumentNullException">null.</exception>
    public static IServiceCollection AddCustomerProjections(this IServiceCollection services, string applicationId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        services.TryAddScoped<IProjectionUpdateHandler<SnapshotEvent>, PartiesSnapshotHandler>();
        services.TryAddScoped<IProjectionUpdateHandler<CustomerInformationChanged>, CustomerInformationChangedProjectionUpdateHandler>();
        services.TryAddScoped<IProjectionUpdateHandler<CustomerRegistered>, CustomerRegisteredProjectionUpdateHandler>();
        services.TryAddScoped<IProjectionUpdateHandler<IntercompanyDropshipDeliveryForCustomerDeselected>, IntercompanyDropshipDeliveryForCustomerDeselectedProjectionUpdateHandler>();
        services.TryAddScoped<IProjectionUpdateHandler<IntercompanyDropshipDeliveryForCustomerSelected>, IntercompanyDropshipDeliveryForCustomerSelectedProjectionUpdateHandler>();
        _ = services.AddActorProjectionFactory<Customer>(applicationId);
        _ = services
         .AddControllers()
         .AddApplicationPart(typeof(CustomerIntegrationEventsController).Assembly)
         .AddDapr();
        return services;
    }
}