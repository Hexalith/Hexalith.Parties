using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.CommandApi.Models;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parties")]
public sealed class PartiesController(
    ICommandRouter commandRouter,
    IHttpClientFactory httpClientFactory,
    ILogger<PartiesController> logger) : ControllerBase
{
    private const string _actorType = "AggregateActor";
    private const string _domain = "party";

    private static readonly JsonSerializerOptions _actorStateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Cross-tenant authorization for opaque IDs (AC #8):
    // Tenant-qualified IDs (e.g., "tenant-b:party:{id}") return 403 when the tenant segment
    // does not match the JWT tenant. For opaque IDs (plain GUIDs), the DAPR actor lookup is
    // always scoped to the JWT tenant, so a foreign-tenant party returns 404—this is intentional
    // to prevent cross-tenant enumeration attacks (disclosing party existence in other tenants).
    // When read-model projections are available (Epic 3), a tenant-aware index can enable 403
    // for opaque IDs without information leakage.
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PartyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetPartyAsync(string id, CancellationToken cancellationToken)
    {
        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string? tenant = ExtractTenant();
        if (tenant is null)
        {
            return CreateUnauthorizedProblemDetails("A valid tenant claim is required to access this resource.", correlationId);
        }

        if (TryParseScopedPartyId(id, out string scopedTenant, out string scopedDomain, out string scopedAggregateId))
        {
            if (!string.Equals(scopedTenant, tenant, StringComparison.Ordinal))
            {
                return CreateForbiddenProblemDetails("Cross-tenant access denied.", correlationId);
            }

            if (!string.Equals(scopedDomain, _domain, StringComparison.OrdinalIgnoreCase))
            {
                return CreateNotFoundProblemDetails(id, correlationId);
            }

            id = scopedAggregateId;
        }

        string actorId = $"{tenant}:{_domain}:{id}";
        string snapshotKey = $"{tenant}:{_domain}:{id}:snapshot";

        logger.LogInformation(
            "Retrieving party: AggregateId={AggregateId}, CorrelationId={CorrelationId}, Tenant={Tenant}",
            id,
            correlationId,
            tenant);

        HttpClient client = httpClientFactory.CreateClient("DaprSidecar");

        HttpResponseMessage response;
        try
        {
            response = await client
                .GetAsync(
                    $"/v1.0/actors/{_actorType}/{Uri.EscapeDataString(actorId)}/state/{Uri.EscapeDataString(snapshotKey)}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to read actor state: AggregateId={AggregateId}, Tenant={Tenant}", id, tenant);
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        if (!response.IsSuccessStatusCode)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using JsonDocument doc = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("State", out JsonElement stateElement)
            || stateElement.ValueKind == JsonValueKind.Null)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        PartyStateSnapshot? state = stateElement.Deserialize<PartyStateSnapshot>(_actorStateJsonOptions);
        if (state is null)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        DateTimeOffset snapshotTime = doc.RootElement.TryGetProperty("CreatedAt", out JsonElement createdAtEl)
            ? createdAtEl.Deserialize<DateTimeOffset>()
            : DateTimeOffset.UtcNow;

        var detail = new PartyDetail
        {
            Id = id,
            Type = state.Type,
            IsActive = state.IsActive,
            DisplayName = state.DisplayName,
            SortName = state.SortName,
            PersonDetails = state.Person,
            OrganizationDetails = state.Organization,
            ContactChannels = state.ContactChannels,
            Identifiers = state.Identifiers,
            CreatedAt = snapshotTime,
            LastModifiedAt = snapshotTime,
        };

        return Ok(detail);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> CreateParty([FromBody] CreateParty command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return DispatchCommandAsync(command.PartyId, nameof(CreateParty), command, cancellationToken);
    }

    [HttpPost("{id}/update-person-details")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> UpdatePersonDetails(string id, [FromBody] UpdatePersonDetails command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.UpdatePersonDetails.PartyId));
        return DispatchCommandAsync(id, nameof(UpdatePersonDetails), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/update-organization-details")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> UpdateOrganizationDetails(string id, [FromBody] UpdateOrganizationDetails command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.UpdateOrganizationDetails.PartyId));
        return DispatchCommandAsync(id, nameof(UpdateOrganizationDetails), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/set-natural-person")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> SetIsNaturalPerson(string id, [FromBody] SetIsNaturalPerson command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.SetIsNaturalPerson.PartyId));
        return DispatchCommandAsync(id, nameof(SetIsNaturalPerson), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> DeactivateParty(string id, CancellationToken cancellationToken)
        => DispatchCommandAsync(id, nameof(Contracts.Commands.DeactivateParty), new DeactivateParty { PartyId = id }, cancellationToken);

    [HttpPost("{id}/reactivate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> ReactivateParty(string id, CancellationToken cancellationToken)
        => DispatchCommandAsync(id, nameof(Contracts.Commands.ReactivateParty), new ReactivateParty { PartyId = id }, cancellationToken);

    private async Task<IActionResult> DispatchCommandAsync<TCommand>(
        string aggregateId,
        string commandType,
        TCommand command,
        CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken).ConfigureAwait(false);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string? tenant = ExtractTenant();
        if (tenant is null)
        {
            return CreateUnauthorizedProblemDetails("A valid tenant claim is required to access this resource.", correlationId);
        }

        string userId = User.FindFirst("sub")?.Value ?? "unknown";

        var submitCommand = new SubmitCommand(
            Tenant: tenant,
            Domain: _domain,
            AggregateId: aggregateId,
            CommandType: commandType,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: correlationId,
            UserId: userId);

        logger.LogInformation(
            "Dispatching command: CommandType={CommandType}, AggregateId={AggregateId}, CorrelationId={CorrelationId}, Tenant={Tenant}",
            commandType,
            aggregateId,
            correlationId,
            tenant);

        CommandProcessingResult result = await commandRouter
            .RouteCommandAsync(submitCommand, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted)
        {
            return CreateDomainRejectionProblemDetails(result.ErrorMessage, correlationId, tenant);
        }

        return Accepted(new { correlationId });
    }

    private string? ExtractTenant()
    {
        return User.FindAll("eventstore:tenant")
            .Select(c => c.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static void EnsureRouteMatchesBodyPartyId(string routeId, string bodyPartyId, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(bodyPartyId))
        {
            return;
        }

        if (!string.Equals(routeId, bodyPartyId, StringComparison.Ordinal))
        {
            throw new ValidationException(
                [
                    new ValidationFailure(propertyName, "PartyId in request body must match route id."),
                ]);
        }
    }

    private static bool TryParseScopedPartyId(string id, out string tenant, out string domain, out string aggregateId)
    {
        tenant = string.Empty;
        domain = string.Empty;
        aggregateId = string.Empty;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        string[] segments = id.Split(':', 3, StringSplitOptions.None);
        if (segments.Length != 3
            || string.IsNullOrWhiteSpace(segments[0])
            || string.IsNullOrWhiteSpace(segments[1])
            || string.IsNullOrWhiteSpace(segments[2]))
        {
            return false;
        }

        tenant = segments[0];
        domain = segments[1];
        aggregateId = segments[2];
        return true;
    }

    private async Task ValidateCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return;
        }

        IValidator<TCommand>? validator = HttpContext.RequestServices.GetService<IValidator<TCommand>>();
        if (validator is null)
        {
            return;
        }

        ValidationResult result = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    private ObjectResult CreateUnauthorizedProblemDetails(string detail, string correlationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Type = "urn:hexalith:parties:error:Unauthorized",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status401Unauthorized };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private ObjectResult CreateForbiddenProblemDetails(string detail, string correlationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = "urn:hexalith:parties:error:Forbidden",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status403Forbidden };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private ObjectResult CreateNotFoundProblemDetails(string partyId, string correlationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Party Not Found",
            Type = "urn:hexalith:parties:error:PartyNotFound",
            Detail = $"No party found with ID '{partyId}'.",
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status404NotFound };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private ObjectResult CreateDomainRejectionProblemDetails(string? errorMessage, string correlationId, string tenant)
    {
        string rejectionType = errorMessage?.Replace("Domain rejection: ", string.Empty, StringComparison.Ordinal) ?? "Unknown";
        string simpleType = rejectionType.Contains('.', StringComparison.Ordinal)
            ? rejectionType[(rejectionType.LastIndexOf('.') + 1)..]
            : rejectionType;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Domain Rejection",
            Type = $"urn:hexalith:parties:rejection:{simpleType}",
            Detail = errorMessage ?? "The command was rejected by domain logic.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["tenantId"] = tenant,
                ["correctiveAction"] = "Adjust the request to satisfy domain rules and retry.",
            },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
