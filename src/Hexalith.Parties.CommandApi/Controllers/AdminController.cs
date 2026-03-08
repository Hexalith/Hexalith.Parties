using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Authentication;
using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.CommandApi.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/v1/admin")]
public sealed class AdminController(
    IProjectionRebuildService rebuildService,
    IPartyKeyManagementService keyManagementService,
    IKeyStorageBackend keyStorageBackend,
    IKeyOperationAuditService keyOperationAuditService,
    PartyErasureOrchestrator erasureOrchestrator,
    ICommandRouter commandRouter,
    ICorrelationContextAccessor correlationContextAccessor,
    ILogger<AdminController> logger) : ControllerBase
{
    private const string Domain = "party";
    private const string TenantClaimType = PartiesClaimsTransformation.TenantClaimType;

    private string? ExtractTenant()
        => User.FindAll(TenantClaimType)
            .Select(c => c.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    [HttpPost("projections/rebuild")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult RebuildProjections([FromBody] RebuildProjectionsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Problem(
                title: "Invalid request",
                detail: "tenantId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string[] validProjections = ["detail", "index", "all"];
        if (!validProjections.Contains(request.Projection, StringComparer.OrdinalIgnoreCase))
        {
            return Problem(
                title: "Invalid request",
                detail: $"projection must be one of: {string.Join(", ", validProjections)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        // Start rebuild in background — don't block the HTTP request
        _ = Task.Run(async () =>
        {
            string? previousCorrelationId = correlationContextAccessor.CorrelationId;
            correlationContextAccessor.CorrelationId = correlationId;
            try
            {
                bool rebuildDetail = string.Equals(request.Projection, "detail", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Projection, "all", StringComparison.OrdinalIgnoreCase);
                bool rebuildIndex = string.Equals(request.Projection, "index", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Projection, "all", StringComparison.OrdinalIgnoreCase);

                if (rebuildDetail)
                {
                    await rebuildService.RebuildDetailProjectionAsync(
                        request.TenantId!, request.PartyId, CancellationToken.None).ConfigureAwait(false);
                }

                if (rebuildIndex)
                {
                    await rebuildService.RebuildIndexProjectionAsync(
                        request.TenantId!, CancellationToken.None).ConfigureAwait(false);
                }

                logger.LogInformation(
                    "Projection rebuild completed. CorrelationId={CorrelationId}, TenantId={TenantId}, Projection={Projection}",
                    correlationId,
                    request.TenantId,
                    request.Projection);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Projection rebuild failed. CorrelationId={CorrelationId}, TenantId={TenantId}, Projection={Projection}",
                    correlationId,
                    request.TenantId,
                    request.Projection);
            }
            finally
            {
                correlationContextAccessor.CorrelationId = previousCorrelationId;
            }
        });

        return Accepted(new { correlationId });
    }

    [HttpPost("parties/{partyId}/rotate-key")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RotateKey(string partyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(
                title: "Invalid request",
                detail: "partyId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(
                title: "Unauthorized",
                detail: "Tenant context is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        string userId = User.FindFirst("sub")?.Value ?? "admin";
        string? previousCorrelationId = correlationContextAccessor.CorrelationId;
        correlationContextAccessor.CorrelationId = correlationId;

        try
        {
            PartyKeyInfo keyInfo = await keyManagementService.RotateKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

            RotatePartyKey rotateCommand = new()
            {
                PartyId = partyId,
                NewKeyVersion = keyInfo.Version,
                PreviousKeyVersion = Math.Max(1, keyInfo.Version - 1),
            };

            var submitCommand = new SubmitCommand(
                Tenant: tenantId,
                Domain: Domain,
                AggregateId: partyId,
                CommandType: nameof(RotatePartyKey),
                Payload: JsonSerializer.SerializeToUtf8Bytes(rotateCommand),
                CorrelationId: correlationId,
                UserId: userId);

            CommandProcessingResult result = await commandRouter
                .RouteCommandAsync(submitCommand, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Accepted)
            {
                logger.LogWarning(
                    "Key rotation event dispatch rejected. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Error={Error}",
                    correlationId,
                    tenantId,
                    partyId,
                    result.ErrorMessage);

                return Problem(
                    title: "Key rotation rejected",
                    detail: result.ErrorMessage ?? "The key rotation command was rejected.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            logger.LogInformation(
                "Key rotation completed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, NewVersion={Version}",
                correlationId,
                tenantId,
                partyId,
                keyInfo.Version);

            return Accepted(new { correlationId = result.CorrelationId ?? correlationId });
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "Key rotation failed because no key exists. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                correlationId,
                tenantId,
                partyId);

            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Key not found",
                Detail = ex.Message,
            });
        }
        finally
        {
            correlationContextAccessor.CorrelationId = previousCorrelationId;
        }
    }

    [HttpGet("parties/{partyId}/key-versions")]
    [ProducesResponseType(typeof(KeyVersionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKeyVersions(string partyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(title: "Unauthorized", detail: "Tenant context is required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        IReadOnlyList<int> versions = await keyStorageBackend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        return Ok(new KeyVersionsResponse
        {
            PartyId = partyId,
            TenantId = tenantId,
            Versions = [.. versions],
        });
    }

    [HttpGet("parties/{partyId}/key-audit-trail")]
    [ProducesResponseType(typeof(IReadOnlyList<KeyOperationAuditEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKeyAuditTrail(string partyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(title: "Unauthorized", detail: "Tenant context is required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        IReadOnlyList<KeyOperationAuditEntry> entries = await keyOperationAuditService
            .GetAuditTrailAsync(tenantId, partyId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(entries);
    }

    [HttpPost("parties/{partyId}/erase")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult EraseParty(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(title: "Unauthorized", detail: "Tenant context is required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        string userId = User.FindFirst("sub")?.Value ?? "admin";

        _ = Task.Run(async () =>
        {
            string? previousCorrelationId = correlationContextAccessor.CorrelationId;
            correlationContextAccessor.CorrelationId = correlationId;
            try
            {
                // Phase 1: Dispatch erasure command to aggregate
                EraseParty command = new() { PartyId = partyId, TenantId = tenantId };
                var submitCommand = new SubmitCommand(
                    Tenant: tenantId,
                    Domain: Domain,
                    AggregateId: partyId,
                    CommandType: nameof(EraseParty),
                    Payload: JsonSerializer.SerializeToUtf8Bytes(command),
                    CorrelationId: correlationId,
                    UserId: userId);

                CommandProcessingResult result = await commandRouter
                    .RouteCommandAsync(submitCommand, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!result.Accepted)
                {
                    logger.LogWarning(
                        "Erasure command rejected. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Error={Error}",
                        correlationId, tenantId, partyId, result.ErrorMessage);
                    return;
                }

                // Phase 2: Key destruction
                ErasureCertificate? certificate = await erasureOrchestrator
                    .ExecuteKeyDestructionAsync(tenantId, partyId, CancellationToken.None)
                    .ConfigureAwait(false);

                if (certificate is null)
                {
                    logger.LogError(
                        "Key destruction failed. Party stays in ErasurePending. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                        correlationId, tenantId, partyId);
                    return;
                }

                // Phase 3: Verification
                ErasureVerificationReport report = await erasureOrchestrator
                    .ExecuteVerificationAsync(tenantId, partyId, certificate, CancellationToken.None)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Erasure completed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Status={Status}",
                    correlationId, tenantId, partyId, report.OverallStatus);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Erasure failed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                    correlationId, tenantId, partyId);
            }
            finally
            {
                correlationContextAccessor.CorrelationId = previousCorrelationId;
            }
        });

        return Accepted(new { correlationId });
    }

    [HttpGet("parties/{partyId}/erasure-status")]
    [ProducesResponseType(typeof(ErasureStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetErasureStatus(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(title: "Unauthorized", detail: "Tenant context is required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // Status is determined by the aggregate state (via projection)
        // For now, return a placeholder — full integration requires querying aggregate state
        return Ok(new ErasureStatusResponse { PartyId = partyId, TenantId = tenantId, Status = "unknown" });
    }

    [HttpGet("parties/{partyId}/erasure-certificate")]
    [ProducesResponseType(typeof(ErasureCertificate), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetErasureCertificate(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(title: "Unauthorized", detail: "Tenant context is required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // Certificate is stored in DAPR state store — requires state store query
        // Placeholder response for endpoint structure
        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Certificate Not Found",
            Detail = $"No erasure certificate found for party '{partyId}'.",
        });
    }

    [HttpPost("parties/{partyId}/retry-verification")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult RetryVerification(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string? tenantId = ExtractTenant();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(title: "Unauthorized", detail: "Tenant context is required.", statusCode: StatusCodes.Status401Unauthorized);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        _ = Task.Run(async () =>
        {
            string? previousCorrelationId = correlationContextAccessor.CorrelationId;
            correlationContextAccessor.CorrelationId = correlationId;
            try
            {
                // Re-create a minimal certificate for verification retry
                ErasureCertificate certificate = new()
                {
                    PartyId = partyId,
                    TenantId = tenantId,
                    Timestamp = DateTimeOffset.UtcNow,
                    KeyVersionsDestroyed = [],
                    VerificationStatus = ErasureVerificationStatus.Pending,
                };

                ErasureVerificationReport report = await erasureOrchestrator
                    .ExecuteVerificationAsync(tenantId, partyId, certificate, CancellationToken.None)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Verification retry completed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Status={Status}",
                    correlationId, tenantId, partyId, report.OverallStatus);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Verification retry failed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                    correlationId, tenantId, partyId);
            }
            finally
            {
                correlationContextAccessor.CorrelationId = previousCorrelationId;
            }
        });

        return Accepted(new { correlationId });
    }
}

public sealed record KeyVersionsResponse
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required IReadOnlyList<int> Versions { get; init; }
}

public sealed record ErasureStatusResponse
{
    public required string PartyId { get; init; }
    public required string TenantId { get; init; }
    public required string Status { get; init; }
}

public sealed record RebuildProjectionsRequest
{
    [Required]
    public string? TenantId { get; init; }

    [Required]
    public string Projection { get; init; } = "all";

    public string? PartyId { get; init; }
}
