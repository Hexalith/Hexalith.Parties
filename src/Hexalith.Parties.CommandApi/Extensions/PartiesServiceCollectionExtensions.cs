using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Actors.Client;

using FluentValidation;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.Parties.CommandApi.Authentication;
using Hexalith.Parties.CommandApi.ErrorHandling;
using Hexalith.Parties.CommandApi.Validation;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Strategies;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Extensions;

public static class PartiesServiceCollectionExtensions
{
    public static IServiceCollection AddParties(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ProblemDetails support (RFC 9457)
        _ = services.AddProblemDetails();

        // Exception handlers (order matters — first match wins)
        _ = services.AddExceptionHandler<PartiesValidationExceptionHandler>();
        _ = services.AddExceptionHandler<PartiesGlobalExceptionHandler>();

        _ = services.AddHttpContextAccessor();

        // JWT Bearer Authentication
        _ = services.AddOptions<PartiesAuthenticationOptions>()
            .BindConfiguration("Authentication:JwtBearer")
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<PartiesAuthenticationOptions>, ValidatePartiesAuthenticationOptions>();
        _ = services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigurePartiesJwtBearerOptions>();

        _ = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        _ = services.AddAuthorization();

        // Claims transformation (tenant extraction from JWT)
        _ = services.AddTransient<IClaimsTransformation, PartiesClaimsTransformation>();

        // EventStore server infrastructure (command routing, actors)
        _ = services.AddEventStoreServer(configuration);

        // OpenAPI document generation
        _ = services.AddOpenApi();

        // Projection infrastructure (Epic 3)
        _ = services.AddSingleton<IIndexPartitionStrategy, SingleKeyPartitionStrategy>();
        _ = services.AddOptions<ProjectionOptions>()
            .Bind(configuration.GetSection(ProjectionOptions.ConfigurationSection))
            .Validate(o => o.BatchSize > 0, "ProjectionOptions.BatchSize must be greater than 0.")
            .Validate(o => o.BatchTimeWindowMs > 0, "ProjectionOptions.BatchTimeWindowMs must be greater than 0.")
            .ValidateOnStart();

        services.AddActors(options =>
        {
            options.Actors.RegisterActor<PartyDetailProjectionActor>();
            options.Actors.RegisterActor<PartyIndexProjectionActor>();
        });

        // Actor proxy factory for querying projection actors
        _ = services.AddSingleton<IActorProxyFactory>(_ => new ActorProxyFactory());

        // FluentValidation (assembly scanning — no explicit validator registration)
        _ = services.AddValidatorsFromAssemblyContaining<CreatePartyValidator>();

        // JSON serialization: camelCase, ISO 8601, string enums, omit nulls
        _ = services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        _ = services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return services;
    }
}
