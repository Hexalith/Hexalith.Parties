using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Security;
using Hexalith.Parties.Contracts.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parties")]
public sealed class PartiesController(
    ICommandRouter commandRouter,
    IActorProxyFactory actorProxyFactory,
    IPersonalDataCommandGuard personalDataCommandGuard,
    IPartySearchService searchService,
    IProjectionUpdateOrchestrator projectionUpdateOrchestrator,
    ILogger<PartiesController> logger) : ControllerBase
{
    private const string _domain = "party";
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

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
        bool isRebuilding = await proxy.IsRebuildingAsync().ConfigureAwait(false);
        IReadOnlyDictionary<string, PartyIndexEntry> entries = await GetPartyIndexEntriesAsync(proxy).ConfigureAwait(false);

        if (isRebuilding)
        {
            SetDegradedHeaders(Response);
        }

        IEnumerable<PartyIndexEntry> filtered = entries.Values.Where(e => !e.IsErased);

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

        return Ok(PartySearchResultsBuilder.BuildPagedList(filtered, null, null, page, pageSize));
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(PartySearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchPartiesAsync(
        [FromQuery] string? q = null,
        [FromQuery] string? mode = null,
        [FromQuery] string? caseId = null,
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

        // Validate caseId — applied as a tenant-scoped filter on Memories candidates.
        // Reject extreme lengths, ANY control character (not just CR/LF), path-traversal
        // segments, and empty strings to defend against header-injection, oversized-input,
        // proxy-collapse and case-isolation-bypass. Real authorization (the caller is allowed
        // to scope to that case id) is the responsibility of upstream auth middleware.
        if (caseId is not null)
        {
            if (caseId.Length == 0
                || caseId.Length > 256
                || ContainsControlCharacter(caseId)
                || caseId.Contains("..", StringComparison.Ordinal))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid caseId.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "caseId must be 1–256 printable characters and must not contain '..' segments.",
                    Type = "urn:hexalith:parties:error:InvalidCaseId",
                });
            }
        }

        // Reject unknown modes explicitly rather than silently degrading. Graph mode requires
        // a graph context that the REST endpoint does not currently expose (use the MCP tool).
        PartySearchMode parsedMode;
        try
        {
            parsedMode = ParseSearchModeStrict(mode);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid search mode.",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message,
                Type = "urn:hexalith:parties:error:InvalidSearchMode",
            });
        }

        if (IsEffectivelyEmptyQuery(q))
        {
            var emptyResult = new PartySearchResponse(
                new PagedResult<PartySearchResult>
                {
                    Items = [],
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 1,
                },
                PartySearchExecutionStatus.LocalOnly,
                "Empty query — list endpoint behaviour applied.",
                ScoreMetadata: [],
                SourceMetadata: []);
            SetSearchMetadataHeaders(Response, emptyResult);
            return Ok(emptyResult);
        }

        var actorId = new ActorId($"{tenant}:party-index");
        IPartyIndexProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            actorId, nameof(PartyIndexProjectionActor));
        bool isRebuilding = await proxy.IsRebuildingAsync().ConfigureAwait(false);
        IReadOnlyDictionary<string, PartyIndexEntry> entries = await GetPartyIndexEntriesAsync(proxy).ConfigureAwait(false);

        if (isRebuilding)
        {
            SetDegradedHeaders(Response);
        }

        IReadOnlyList<PartyIndexEntry> visibleEntries = [.. entries.Values.Where(e => e is not null && !e.IsErased)];
        HashSet<string> authorizedPartyIds = visibleEntries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        PartySearchResponse search = await searchService.SearchAsync(
            new PartySearchRequest(
                tenant,
                q,
                parsedMode,
                TypeFilter: null,
                ActiveFilter: null,
                page,
                pageSize,
                CaseId: caseId,
                AuthorizedPartyIds: authorizedPartyIds),
            visibleEntries,
            HttpContext.RequestAborted).ConfigureAwait(false);

        SetSearchMetadataHeaders(Response, search);
        return Ok(search);
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
        bool isRebuilding = await proxy.IsRebuildingAsync().ConfigureAwait(false);
        PartyDetail? detail = await proxy.ReadDetailAsync().ConfigureAwait(false);

        if (isRebuilding)
        {
            SetDegradedHeaders(Response);
            return new JsonResult(detail)
            {
                StatusCode = StatusCodes.Status200OK,
            };
        }

        if (detail is null)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        if (detail.IsErased)
        {
            return CreateErasedProblemDetails(id, correlationId, detail.ErasedAt);
        }

        return Ok(detail);
    }

    [HttpGet("{id}/name")]
    [ProducesResponseType(typeof(TemporalNameResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone, "application/problem+json")]
    public async Task<IActionResult> GetPartyNameAtAsync(string id, [FromQuery] DateTimeOffset at)
    {
        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string? tenant = ExtractTenant();
        if (tenant is null)
        {
            return CreateUnauthorizedProblemDetails("A valid tenant claim is required to access this resource.", correlationId);
        }

        var actorId = new ActorId($"{tenant}:party-detail:{id}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? detail = await proxy.ReadDetailAsync().ConfigureAwait(false);

        if (detail is null)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        if (detail.IsErased)
        {
            return CreateErasedProblemDetails(id, correlationId, detail.ErasedAt);
        }

        if (detail.NameHistory.Count == 0)
        {
            return CreateNameHistoryUnavailableProblemDetails(id, correlationId);
        }

        NameHistoryEntry? entry = detail.NameHistory
            .Where(e => e.ChangedAt <= at)
            .OrderBy(e => e.ChangedAt)
            .LastOrDefault();

        if (entry is null)
        {
            return CreateNameNotFoundAtTimestampProblemDetails(id, at, correlationId);
        }

        return Ok(new TemporalNameResult
        {
            PartyId = id,
            AsOf = at,
            DisplayName = entry.DisplayName,
            SortName = entry.SortName,
        });
    }

    [HttpGet("{id}/name-history")]
    [ProducesResponseType(typeof(IReadOnlyList<NameHistoryEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone, "application/problem+json")]
    public async Task<IActionResult> GetPartyNameHistoryAsync(string id)
    {
        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string? tenant = ExtractTenant();
        if (tenant is null)
        {
            return CreateUnauthorizedProblemDetails("A valid tenant claim is required to access this resource.", correlationId);
        }

        var actorId = new ActorId($"{tenant}:party-detail:{id}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? detail = await proxy.ReadDetailAsync().ConfigureAwait(false);

        if (detail is null)
        {
            return CreateNotFoundProblemDetails(id, correlationId);
        }

        if (detail.IsErased)
        {
            return CreateErasedProblemDetails(id, correlationId, detail.ErasedAt);
        }

        return Ok(detail.NameHistory);
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
    public Task<IActionResult> UpdatePersonDetails(string id, [FromBody] JsonElement commandBody, CancellationToken cancellationToken)
    {
        UpdatePersonDetails command = ParseUpdatePersonDetails(id, commandBody);
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

        string? blockingReason = await personalDataCommandGuard
            .GetBlockingReasonAsync(tenant, aggregateId, command!, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(blockingReason))
        {
            return CreateCryptoUnavailableProblemDetails(blockingReason, correlationId, tenant);
        }

        string userId = User.FindFirst("sub")?.Value ?? "unknown";

        var submitCommand = new SubmitCommand(
            MessageId: Guid.NewGuid().ToString(),
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

        await TryUpdateProjectionAsync(tenant, aggregateId, correlationId, cancellationToken).ConfigureAwait(false);

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

        string? blockingReason = await personalDataCommandGuard
            .GetBlockingReasonAsync(tenant, aggregateId, command!, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(blockingReason))
        {
            return CreateCryptoUnavailableProblemDetails(blockingReason, correlationId, tenant);
        }

        string userId = User.FindFirst("sub")?.Value ?? "unknown";

        var submitCommand = new SubmitCommand(
            MessageId: Guid.NewGuid().ToString(),
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

        await TryUpdateProjectionAsync(tenant, aggregateId, correlationId, cancellationToken).ConfigureAwait(false);

        return Accepted(new { correlationId = result.CorrelationId ?? correlationId });
    }

    private async Task TryUpdateProjectionAsync(
        string tenant,
        string aggregateId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await projectionUpdateOrchestrator
                .UpdateProjectionAsync(new AggregateIdentity(tenant, _domain, aggregateId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Synchronous projection update failed after accepted command: AggregateId={AggregateId}, CorrelationId={CorrelationId}, Tenant={Tenant}",
                aggregateId,
                correlationId,
                tenant);

            // Surface the failure to the client so callers depending on read-after-write
            // consistency know the synchronous projection did not complete and can choose
            // to poll or retry. The 202 Accepted contract still holds — the command itself
            // was accepted by the aggregate.
            HttpContext.Response.Headers["X-Parties-Projection-Sync"] = "failed";
        }
    }

    private static UpdatePersonDetails ParseUpdatePersonDetails(string routeId, JsonElement body)
    {
        if (TryGetProperty(body, "personDetails", out JsonElement personDetailsElement))
        {
            UpdatePersonDetails? command = body.Deserialize<UpdatePersonDetails>(s_jsonOptions)
                ?? throw new ValidationException(
                    [
                        new ValidationFailure(nameof(UpdatePersonDetails), "Request body is required."),
                    ]);

            // The new-shape branch did not previously validate that PersonDetails is non-null.
            // A body like { "partyId": "x", "personDetails": null } would deserialize to a
            // record with PersonDetails = null and silently flow into the dispatcher.
            if (command.PersonDetails is null)
            {
                throw new ValidationException(
                    [
                        new ValidationFailure("personDetails", "personDetails must be a non-null object."),
                    ]);
            }

            return command;
        }

        string partyId = TryGetProperty(body, "partyId", out JsonElement partyIdElement)
            ? partyIdElement.GetString() ?? routeId
            : routeId;
        string? firstName = TryGetProperty(body, "firstName", out JsonElement firstNameElement)
            ? firstNameElement.GetString()
            : null;
        string? lastName = TryGetProperty(body, "lastName", out JsonElement lastNameElement)
            ? lastNameElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new ValidationException(
                    [
                    new ValidationFailure("PersonDetails", "PersonDetails or firstName/lastName is required."),
                ]);
        }

        DateTimeOffset? dateOfBirth = null;
        if (TryGetProperty(body, "dateOfBirth", out JsonElement dateOfBirthElement)
            && dateOfBirthElement.ValueKind != JsonValueKind.Null)
        {
            // GetDateTimeOffset throws FormatException / InvalidOperationException for
            // unparseable values; surface those as 400-class ValidationException so the
            // global handler returns a structured ProblemDetails rather than 500.
            try
            {
                dateOfBirth = dateOfBirthElement.GetDateTimeOffset();
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                throw new ValidationException(
                    [
                        new ValidationFailure("dateOfBirth", $"dateOfBirth is not a valid ISO-8601 date/time: {ex.Message}"),
                    ]);
            }
        }

        string? prefix = TryGetProperty(body, "prefix", out JsonElement prefixElement)
            ? prefixElement.GetString()
            : null;
        string? suffix = TryGetProperty(body, "suffix", out JsonElement suffixElement)
            ? suffixElement.GetString()
            : null;

        return new UpdatePersonDetails
        {
            PartyId = partyId,
            PersonDetails = new PersonDetails
            {
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dateOfBirth,
                Prefix = prefix,
                Suffix = suffix,
            },
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static async Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetPartyIndexEntriesAsync(IPartyIndexProjectionActor proxy)
    {
        Task<string?>? jsonTask = null;
        try
        {
            jsonTask = proxy.GetEntriesJsonAsync();
        }
        catch (NotImplementedException)
        {
            // Older test doubles and actor implementations can still use the typed actor method.
        }

        if (jsonTask is not null)
        {
            string? json = await jsonTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                Dictionary<string, PartyIndexEntry>? entries =
                    JsonSerializer.Deserialize<Dictionary<string, PartyIndexEntry>>(json, s_jsonOptions);
                if (entries is not null)
                {
                    return entries;
                }
            }
        }

        return await proxy.GetEntriesAsync().ConfigureAwait(false);
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

    private static void SetDegradedHeaders(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Headers["X-Service-Degraded"] = "true";
        response.Headers["X-Stale-Data-Age"] = "0";
    }

    private static void SetSearchMetadataHeaders(HttpResponse response, PartySearchResponse search)
    {
        response.Headers["X-Parties-Search-Status"] = search.Status.ToString();
        if (!string.IsNullOrWhiteSpace(search.DegradedReason))
        {
            // Strip CR/LF so a Memories-side reason containing newlines cannot trigger a
            // header-injection / Kestrel-validation 500.
            response.Headers["X-Parties-Search-Degraded-Reason"] =
                search.DegradedReason.Replace('\r', ' ').Replace('\n', ' ');
        }
    }

    private static PartySearchMode ParseSearchMode(string? mode)
        => mode?.Trim().ToLowerInvariant() switch
        {
            "lexical" or "syntactic" => PartySearchMode.Lexical,
            "semantic" => PartySearchMode.Semantic,
            "graph" => PartySearchMode.Graph,
            _ => PartySearchMode.Hybrid,
        };

    private static PartySearchMode ParseSearchModeStrict(string? mode)
    {
        if (mode is null)
        {
            return PartySearchMode.Hybrid;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "" or "hybrid" => PartySearchMode.Hybrid,
            "lexical" or "syntactic" => PartySearchMode.Lexical,
            "semantic" => PartySearchMode.Semantic,
            // The REST endpoint does not currently expose a graph context channel; graph mode
            // is reachable via the find_parties MCP tool which accepts explicit context.
            "graph" => throw new ArgumentException(
                "Graph mode is not supported on the REST search endpoint. Use the find_parties MCP tool with explicit graph context."),
            _ => throw new ArgumentException($"Unknown search mode '{mode}'. Allowed: hybrid, lexical, semantic."),
        };
    }

    private static bool IsEffectivelyEmptyQuery([NotNullWhen(false)] string? q)
    {
        if (string.IsNullOrEmpty(q))
        {
            return true;
        }

        // Strip zero-width and bidi controls before checking for whitespace so that a query
        // composed only of invisible characters (which `IsNullOrWhiteSpace` does not treat as
        // whitespace) is also rejected as empty.
        foreach (char c in q)
        {
            if (c is '​' or '‌' or '‍' or '‎' or '‏' or '﻿')
            {
                continue;
            }

            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsControlCharacter(ReadOnlySpan<char> value)
    {
        foreach (char c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
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

    private ObjectResult CreateErasedProblemDetails(string partyId, string correlationId, DateTimeOffset? erasedAt)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status410Gone,
            Title = "Party Erased",
            Type = "urn:hexalith:parties:error:PartyErased",
            Detail = $"Party '{partyId}' has been erased under GDPR Article 17.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["erasedAt"] = erasedAt?.ToString("O"),
            },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status410Gone };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private ObjectResult CreateNameNotFoundAtTimestampProblemDetails(string partyId, DateTimeOffset at, string correlationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Name Not Found at Timestamp",
            Type = "urn:hexalith:parties:error:NameNotFoundAtTimestamp",
            Detail = "Party did not exist at the requested timestamp.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["partyId"] = partyId,
                ["requestedTimestamp"] = at.ToString("O"),
            },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status404NotFound };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private ObjectResult CreateNameHistoryUnavailableProblemDetails(string partyId, string correlationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Name History Unavailable",
            Type = "urn:hexalith:parties:error:NameHistoryUnavailable",
            Detail = "Name history not available. Trigger projection rebuild.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["partyId"] = partyId,
            },
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

    private ObjectResult CreateCryptoUnavailableProblemDetails(string detail, string correlationId, string tenant)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Personal Data Write Blocked",
            Type = "urn:hexalith:parties:error:CryptoUnavailable",
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["tenantId"] = tenant,
            },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
