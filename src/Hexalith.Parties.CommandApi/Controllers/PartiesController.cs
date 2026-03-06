using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parties")]
public sealed class PartiesController(
    ICommandRouter commandRouter,
    IActorProxyFactory actorProxyFactory,
    ILogger<PartiesController> logger) : ControllerBase
{
    private const string _domain = "party";

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PartyIndexEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListPartiesAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] bool? active = null,
        [FromQuery] DateTimeOffset? createdAfter = null,
        [FromQuery] DateTimeOffset? createdBefore = null,
        [FromQuery] DateTimeOffset? modifiedAfter = null,
        [FromQuery] DateTimeOffset? modifiedBefore = null)
    {
        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string? tenant = ExtractTenant();
        if (tenant is null)
        {
            return CreateUnauthorizedProblemDetails("A valid tenant claim is required to access this resource.", correlationId);
        }

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 1;
        }
        else if (pageSize > 100)
        {
            pageSize = 100;
        }

        PartyType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<PartyType>(type, ignoreCase: true, out PartyType parsed))
        {
            typeFilter = parsed;
        }

        var actorId = new ActorId($"{tenant}:party-index");
        IPartyIndexProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            actorId, nameof(PartyIndexProjectionActor));
        IReadOnlyDictionary<string, PartyIndexEntry> entries = await proxy.GetEntriesAsync().ConfigureAwait(false);

        IEnumerable<PartyIndexEntry> filtered = entries.Values;

        if (typeFilter is not null)
        {
            filtered = filtered.Where(e => e.Type == typeFilter.Value);
        }

        if (active is not null)
        {
            filtered = filtered.Where(e => e.IsActive == active.Value);
        }

        if (createdAfter is not null)
        {
            filtered = filtered.Where(e => e.CreatedAt >= createdAfter.Value);
        }

        if (createdBefore is not null)
        {
            filtered = filtered.Where(e => e.CreatedAt <= createdBefore.Value);
        }

        if (modifiedAfter is not null)
        {
            filtered = filtered.Where(e => e.LastModifiedAt >= modifiedAfter.Value);
        }

        if (modifiedBefore is not null)
        {
            filtered = filtered.Where(e => e.LastModifiedAt <= modifiedBefore.Value);
        }

        List<PartyIndexEntry> sorted = [.. filtered.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)];
        int totalCount = sorted.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);
        List<PartyIndexEntry> items = [.. sorted.Skip((page - 1) * pageSize).Take(pageSize)];

        var result = new PagedResult<PartyIndexEntry>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };

        return Ok(result);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<PartySearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchPartiesAsync(
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string? tenant = ExtractTenant();
        if (tenant is null)
        {
            return CreateUnauthorizedProblemDetails("A valid tenant claim is required to access this resource.", correlationId);
        }

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 1;
        }
        else if (pageSize > 100)
        {
            pageSize = 100;
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            var emptyResult = new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 1,
            };
            return Ok(emptyResult);
        }

        var actorId = new ActorId($"{tenant}:party-index");
        IPartyIndexProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            actorId, nameof(PartyIndexProjectionActor));
        IReadOnlyDictionary<string, PartyIndexEntry> entries = await proxy.GetEntriesAsync().ConfigureAwait(false);

        List<(PartySearchResult Result, int Priority)> matches = [];

        foreach (PartyIndexEntry entry in entries.Values)
        {
            if (string.Equals(entry.DisplayName, q, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((new PartySearchResult
                {
                    Party = entry,
                    Matches = [new MatchMetadata { MatchedField = "displayName", MatchType = "exact" }],
                }, 0));
            }
            else if (entry.DisplayName.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((new PartySearchResult
                {
                    Party = entry,
                    Matches = [new MatchMetadata { MatchedField = "displayName", MatchType = "prefix" }],
                }, 1));
            }
            else if (entry.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((new PartySearchResult
                {
                    Party = entry,
                    Matches = [new MatchMetadata { MatchedField = "displayName", MatchType = "contains" }],
                }, 2));
            }
        }

        List<PartySearchResult> sorted = [.. matches
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.Result.Party.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(m => m.Result)];

        int totalCount = sorted.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);
        List<PartySearchResult> items = [.. sorted.Skip((page - 1) * pageSize).Take(pageSize)];

        var result = new PagedResult<PartySearchResult>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };

        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PartyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetPartyAsync(string id)
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

        logger.LogInformation(
            "Retrieving party: AggregateId={AggregateId}, CorrelationId={CorrelationId}, Tenant={Tenant}",
            id,
            correlationId,
            tenant);

        var actorId = new ActorId($"{tenant}:party-detail:{id}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));
        PartyDetail? detail = await proxy.GetDetailAsync().ConfigureAwait(false);

        if (detail is null)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

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

    [HttpPost("{id}/add-contact-channel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> AddContactChannel(string id, [FromBody] AddContactChannel command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.AddContactChannel.PartyId));
        return DispatchCommandAsync(id, nameof(AddContactChannel), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/update-contact-channel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> UpdateContactChannel(string id, [FromBody] UpdateContactChannel command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.UpdateContactChannel.PartyId));
        return DispatchCommandAsync(id, nameof(UpdateContactChannel), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/remove-contact-channel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> RemoveContactChannel(string id, [FromBody] RemoveContactChannel command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.RemoveContactChannel.PartyId));
        return DispatchCommandAsync(id, nameof(RemoveContactChannel), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/add-identifier")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> AddIdentifier(string id, [FromBody] AddIdentifier command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.AddIdentifier.PartyId));
        return DispatchCommandAsync(id, nameof(AddIdentifier), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("{id}/remove-identifier")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> RemoveIdentifier(string id, [FromBody] RemoveIdentifier command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.RemoveIdentifier.PartyId));
        return DispatchCommandAsync(id, nameof(RemoveIdentifier), command with { PartyId = id }, cancellationToken);
    }

    [HttpPost("create-composite")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> CreatePartyComposite([FromBody] CreatePartyComposite command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return DispatchCompositeCommandAsync(command.PartyId, nameof(CreatePartyComposite), command, cancellationToken);
    }

    [HttpPost("{id}/update-composite")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public Task<IActionResult> UpdatePartyComposite(string id, [FromBody] UpdatePartyComposite command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureRouteMatchesBodyPartyId(id, command.PartyId, nameof(Contracts.Commands.UpdatePartyComposite.PartyId));
        return DispatchCompositeCommandAsync(id, nameof(UpdatePartyComposite), command with { PartyId = id }, cancellationToken);
    }

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

    private async Task<IActionResult> DispatchCompositeCommandAsync<TCommand>(
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
            "Dispatching composite command: CommandType={CommandType}, AggregateId={AggregateId}, CorrelationId={CorrelationId}, Tenant={Tenant}",
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

        if (!string.IsNullOrEmpty(result.ResultPayload))
        {
            JsonNode? payload = JsonNode.Parse(result.ResultPayload);
            if (payload is not null)
            {
                payload["correlationId"] = correlationId;
                return new ObjectResult(payload) { StatusCode = StatusCodes.Status202Accepted };
            }
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
