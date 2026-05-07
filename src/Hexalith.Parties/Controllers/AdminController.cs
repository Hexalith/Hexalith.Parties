using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Authentication;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Middleware;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/v1/admin")]
public sealed class AdminController(
    IProjectionRebuildService rebuildService,
    IPartyKeyManagementService keyManagementService,
    IKeyStorageBackend keyStorageBackend,
    IKeyOperationAuditService keyOperationAuditService,
    IPartyErasureRecordStore erasureRecordStore,
    PartyErasureOrchestrator erasureOrchestrator,
    ICommandRouter commandRouter,
    IActorProxyFactory actorProxyFactory,
    ICorrelationContextAccessor correlationContextAccessor,
    ITenantAccessService tenantAccessService,
    ILogger<AdminController> logger) : ControllerBase {
    private const string Domain = "party";
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private string? ExtractTenant() => PartiesAuthClaims.ExtractTenant(User);

    private string? ExtractUserId() => PartiesAuthClaims.ExtractUserId(User);

    private async Task<IActionResult?> AuthorizeAdminAccessAsync(
        string? tenantId,
        string correlationId,
        CancellationToken cancellationToken) {
        TenantAccessDecision decision = await tenantAccessService
            .CheckAccessAsync(tenantId, ExtractUserId(), TenantAccessRequirement.Admin, cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsAllowed) {
            return null;
        }

        logger.LogWarning(
            "Tenant admin access denied. CorrelationId={CorrelationId}, TenantId={TenantId}, ReasonCode={ReasonCode}",
            correlationId,
            tenantId,
            TenantAccessDenialTranslator.ToReasonCode(decision.Reason));

        return TenantAccessDenialTranslator.ToProblemDetails(decision, HttpContext.Request.Path, correlationId);
    }

    // Caller pattern: var (denial, tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(...);
    // If denial is non-null, return it. Otherwise tenantId is guaranteed non-null/non-whitespace
    // because the access service rejects MissingTenantId before returning Allowed.
    private async Task<(IActionResult? Denial, string TenantId)> AuthorizeAdminAccessAndExtractTenantAsync(
        string correlationId,
        CancellationToken cancellationToken) {
        string? tenantId = ExtractTenant();
        IActionResult? denial = await AuthorizeAdminAccessAsync(tenantId, correlationId, cancellationToken).ConfigureAwait(false);
        return (denial, tenantId ?? string.Empty);
    }

    [HttpPost("projections/rebuild")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RebuildProjections([FromBody] RebuildProjectionsRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        // AC5 + D1 resolution: authorization is the gate. We authorize on the
        // JWT-extracted tenant (trusted context), not on `request.TenantId`,
        // and then require `request.TenantId == JWT tenant` so the body cannot
        // redirect the rebuild to a different tenant. Cross-tenant projection
        // rebuild requires re-issuing a token with the target tenant context.
        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        (IActionResult? accessDenied, string trustedTenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        if (string.IsNullOrWhiteSpace(request.TenantId)) {
            return Problem(
                title: "Invalid request",
                detail: "tenantId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.Equals(request.TenantId, trustedTenantId, StringComparison.Ordinal)) {
            return TenantAccessDenialTranslator.ToPayloadTenantConflictProblemDetails(
                HttpContext.Request.Path,
                correlationId);
        }

        string[] validProjections = ["detail", "index", "all"];
        if (!validProjections.Contains(request.Projection, StringComparer.OrdinalIgnoreCase)) {
            return Problem(
                title: "Invalid request",
                detail: $"projection must be one of: {string.Join(", ", validProjections)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Start rebuild in background — don't block the HTTP request
        _ = Task.Run(async () => {
            string? previousCorrelationId = correlationContextAccessor.CorrelationId;
            correlationContextAccessor.CorrelationId = correlationId;
            try {
                bool rebuildDetail = string.Equals(request.Projection, "detail", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Projection, "all", StringComparison.OrdinalIgnoreCase);
                bool rebuildIndex = string.Equals(request.Projection, "index", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Projection, "all", StringComparison.OrdinalIgnoreCase);

                if (rebuildDetail) {
                    await rebuildService.RebuildDetailProjectionAsync(
                        request.TenantId!, request.PartyId, CancellationToken.None).ConfigureAwait(false);
                }

                if (rebuildIndex) {
                    await rebuildService.RebuildIndexProjectionAsync(
                        request.TenantId!, CancellationToken.None).ConfigureAwait(false);
                }

                logger.LogInformation(
                    "Projection rebuild completed. CorrelationId={CorrelationId}, TenantId={TenantId}, Projection={Projection}",
                    correlationId,
                    request.TenantId,
                    request.Projection);
            }
            catch (Exception ex) {
                logger.LogError(
                    ex,
                    "Projection rebuild failed. CorrelationId={CorrelationId}, TenantId={TenantId}, Projection={Projection}",
                    correlationId,
                    request.TenantId,
                    request.Projection);
            }
            finally {
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
    public async Task<IActionResult> RotateKey(string partyId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(
                title: "Invalid request",
                detail: "partyId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        string userId = ExtractUserId()!;
        string? previousCorrelationId = correlationContextAccessor.CorrelationId;
        correlationContextAccessor.CorrelationId = correlationId;

        try {
            PartyKeyInfo keyInfo = await keyManagementService.RotateKeyAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);

            RotatePartyKey rotateCommand = new() {
                PartyId = partyId,
                NewKeyVersion = keyInfo.Version,
                PreviousKeyVersion = Math.Max(1, keyInfo.Version - 1),
            };

            var submitCommand = new SubmitCommand(
                MessageId: Guid.NewGuid().ToString(),
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

            if (!result.Accepted) {
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
        catch (KeyNotFoundException ex) {
            logger.LogWarning(
                ex,
                "Key rotation failed because no key exists. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                correlationId,
                tenantId,
                partyId);

            return NotFound(new ProblemDetails {
                Status = StatusCodes.Status404NotFound,
                Title = "Key not found",
                Detail = ex.Message,
            });
        }
        finally {
            correlationContextAccessor.CorrelationId = previousCorrelationId;
        }
    }

    [HttpGet("parties/{partyId}/key-versions")]
    [ProducesResponseType(typeof(KeyVersionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKeyVersions(string partyId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        IReadOnlyList<int> versions = await keyStorageBackend.ListKeyVersionsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        return Ok(new KeyVersionsResponse {
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
    public async Task<IActionResult> GetKeyAuditTrail(string partyId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
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
    public async Task<IActionResult> EraseParty(string partyId) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        string userId = ExtractUserId()!;

        _ = Task.Run(async () => {
            string? previousCorrelationId = correlationContextAccessor.CorrelationId;
            correlationContextAccessor.CorrelationId = correlationId;
            try {
                // Phase 1: Dispatch erasure command to aggregate
                EraseParty command = new() { PartyId = partyId, TenantId = tenantId };
                var submitCommand = new SubmitCommand(
                    MessageId: Guid.NewGuid().ToString(),
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

                if (!result.Accepted) {
                    logger.LogWarning(
                        "Erasure command rejected. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Error={Error}",
                        correlationId, tenantId, partyId, result.ErrorMessage);
                    await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                        PartyId = partyId,
                        TenantId = tenantId,
                        Status = "Rejected",
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ErrorMessage = result.ErrorMessage ?? "The erasure command was rejected.",
                    }).ConfigureAwait(false);
                    return;
                }

                await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                    PartyId = partyId,
                    TenantId = tenantId,
                    Status = ErasureStatus.ErasurePending.ToString(),
                    UpdatedAt = DateTimeOffset.UtcNow,
                }).ConfigureAwait(false);

                // Phase 2: Key destruction
                ErasureCertificate? certificate = await erasureOrchestrator
                    .ExecuteKeyDestructionAsync(tenantId, partyId, CancellationToken.None)
                    .ConfigureAwait(false);

                if (certificate is null) {
                    await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                        PartyId = partyId,
                        TenantId = tenantId,
                        Status = "Key destruction failed — retry or escalate",
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ErrorMessage = "Key destruction failed after the configured retry policy was exhausted.",
                    }).ConfigureAwait(false);

                    logger.LogError(
                        "Key destruction failed. Party stays in ErasurePending. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                        correlationId, tenantId, partyId);
                    return;
                }

                await erasureRecordStore.SaveCertificateAsync(certificate).ConfigureAwait(false);
                await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                    PartyId = partyId,
                    TenantId = tenantId,
                    Status = ErasureStatus.KeyDestroyed.ToString(),
                    UpdatedAt = certificate.Timestamp,
                }).ConfigureAwait(false);

                CommandProcessingResult keyDeletedResult = await SubmitInternalCommandAsync(
                    tenantId,
                    partyId,
                    nameof(MarkPartyEncryptionKeyDeleted),
                    new MarkPartyEncryptionKeyDeleted {
                        PartyId = partyId,
                        TenantId = tenantId,
                        DeletedAt = certificate.Timestamp,
                    },
                    correlationId,
                    userId,
                    CancellationToken.None).ConfigureAwait(false);

                if (!keyDeletedResult.Accepted) {
                    await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                        PartyId = partyId,
                        TenantId = tenantId,
                        Status = ErasureStatus.ErasurePending.ToString(),
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ErrorMessage = keyDeletedResult.ErrorMessage ?? "Failed to update aggregate after key destruction.",
                    }).ConfigureAwait(false);
                    return;
                }

                // Phase 3: Verification
                ErasureVerificationReport report = await erasureOrchestrator
                    .ExecuteVerificationAsync(tenantId, partyId, certificate, CancellationToken.None)
                    .ConfigureAwait(false);

                await erasureRecordStore.SaveVerificationReportAsync(report).ConfigureAwait(false);

                string verificationStatus = report.OverallStatus switch {
                    ErasureVerificationOverallStatus.Complete => ErasureStatus.Verified.ToString(),
                    ErasureVerificationOverallStatus.Partial => "VerificationPartial",
                    ErasureVerificationOverallStatus.Failed => "VerificationFailed",
                    _ => "VerificationUnknown",
                };

                await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                    PartyId = partyId,
                    TenantId = tenantId,
                    Status = verificationStatus,
                    UpdatedAt = report.Timestamp,
                    ErrorMessage = report.OverallStatus == ErasureVerificationOverallStatus.Complete
                        ? null
                        : "Verification did not complete successfully across all stores.",
                }).ConfigureAwait(false);

                if (report.OverallStatus == ErasureVerificationOverallStatus.Complete) {
                    CommandProcessingResult verifiedResult = await SubmitInternalCommandAsync(
                        tenantId,
                        partyId,
                        nameof(MarkErasureVerified),
                        new MarkErasureVerified {
                            PartyId = partyId,
                            TenantId = tenantId,
                            VerifiedAt = report.Timestamp,
                            VerificationReportId = $"{tenantId}:erasure-report:{partyId}",
                        },
                        correlationId,
                        userId,
                        CancellationToken.None).ConfigureAwait(false);

                    if (!verifiedResult.Accepted) {
                        await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                            PartyId = partyId,
                            TenantId = tenantId,
                            Status = ErasureStatus.KeyDestroyed.ToString(),
                            UpdatedAt = DateTimeOffset.UtcNow,
                            ErrorMessage = verifiedResult.ErrorMessage ?? "Failed to mark erasure verification in the aggregate.",
                        }).ConfigureAwait(false);
                        return;
                    }

                    DateTimeOffset erasedAt = DateTimeOffset.UtcNow;
                    CommandProcessingResult erasedResult = await SubmitInternalCommandAsync(
                        tenantId,
                        partyId,
                        nameof(CompletePartyErasure),
                        new CompletePartyErasure {
                            PartyId = partyId,
                            TenantId = tenantId,
                            ErasedAt = erasedAt,
                        },
                        correlationId,
                        userId,
                        CancellationToken.None).ConfigureAwait(false);

                    if (erasedResult.Accepted) {
                        await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                            PartyId = partyId,
                            TenantId = tenantId,
                            Status = ErasureStatus.Erased.ToString(),
                            UpdatedAt = erasedAt,
                            ErasedAt = erasedAt,
                        }).ConfigureAwait(false);
                    }
                    else {
                        await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                            PartyId = partyId,
                            TenantId = tenantId,
                            Status = ErasureStatus.Verified.ToString(),
                            UpdatedAt = DateTimeOffset.UtcNow,
                            ErrorMessage = erasedResult.ErrorMessage ?? "Failed to mark the aggregate as erased.",
                        }).ConfigureAwait(false);
                        return;
                    }
                }

                logger.LogInformation(
                    "Erasure completed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Status={Status}",
                    correlationId, tenantId, partyId, report.OverallStatus);
            }
            catch (Exception ex) {
                await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                    PartyId = partyId,
                    TenantId = tenantId,
                    Status = "Failed",
                    UpdatedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = ex.Message,
                }).ConfigureAwait(false);
                logger.LogError(
                    ex,
                    "Erasure failed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                    correlationId, tenantId, partyId);
            }
            finally {
                correlationContextAccessor.CorrelationId = previousCorrelationId;
            }
        });

        return Accepted(new { correlationId });
    }

    [HttpGet("parties/{partyId}/erasure-status")]
    [ProducesResponseType(typeof(ErasureStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetErasureStatus(string partyId) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        PartyErasureStatusRecord? status = await erasureRecordStore
            .GetStatusAsync(tenantId, partyId, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        ErasureVerificationReport? report = await erasureRecordStore
            .GetVerificationReportAsync(tenantId, partyId, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        return Ok(new ErasureStatusResponse {
            PartyId = partyId,
            TenantId = tenantId,
            Status = status?.Status ?? "unknown",
            UpdatedAt = status?.UpdatedAt,
            ErasedAt = status?.ErasedAt,
            ErrorMessage = status?.ErrorMessage,
            StoreResults = report?.StoreResults,
        });
    }

    [HttpGet("parties/{partyId}/erasure-certificate")]
    [ProducesResponseType(typeof(ErasureCertificateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetErasureCertificate(string partyId) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        ErasureCertificate? certificate = await erasureRecordStore
            .GetCertificateAsync(tenantId, partyId, HttpContext.RequestAborted)
            .ConfigureAwait(false);
        ErasureVerificationReport? report = await erasureRecordStore
            .GetVerificationReportAsync(tenantId, partyId, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (certificate is not null) {
            return Ok(new ErasureCertificateResponse {
                Certificate = certificate,
                VerificationReport = report,
            });
        }

        return NotFound(new ProblemDetails {
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
    public async Task<IActionResult> RetryVerification(string partyId) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        _ = Task.Run(async () => {
            string? previousCorrelationId = correlationContextAccessor.CorrelationId;
            correlationContextAccessor.CorrelationId = correlationId;
            try {
                // Re-create a minimal certificate for verification retry
                ErasureCertificate certificate = await erasureRecordStore
                    .GetCertificateAsync(tenantId, partyId, CancellationToken.None)
                    .ConfigureAwait(false)
                    ?? new ErasureCertificate {
                        PartyId = partyId,
                        TenantId = tenantId,
                        Timestamp = DateTimeOffset.UtcNow,
                        KeyVersionsDestroyed = [],
                        VerificationStatus = ErasureVerificationStatus.Pending,
                    };

                ErasureVerificationReport report = await erasureOrchestrator
                    .ExecuteVerificationAsync(tenantId, partyId, certificate, CancellationToken.None)
                    .ConfigureAwait(false);

                await erasureRecordStore.SaveVerificationReportAsync(report).ConfigureAwait(false);
                await erasureRecordStore.SaveStatusAsync(new PartyErasureStatusRecord {
                    PartyId = partyId,
                    TenantId = tenantId,
                    Status = report.OverallStatus == ErasureVerificationOverallStatus.Complete
                        ? ErasureStatus.Verified.ToString()
                        : "VerificationPartial",
                    UpdatedAt = report.Timestamp,
                    ErrorMessage = report.OverallStatus == ErasureVerificationOverallStatus.Complete
                        ? null
                        : "Verification retry still reports failed or skipped stores.",
                }).ConfigureAwait(false);

                logger.LogInformation(
                    "Verification retry completed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, Status={Status}",
                    correlationId, tenantId, partyId, report.OverallStatus);
            }
            catch (Exception ex) {
                logger.LogError(
                    ex,
                    "Verification retry failed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                    correlationId, tenantId, partyId);
            }
            finally {
                correlationContextAccessor.CorrelationId = previousCorrelationId;
            }
        });

        return Accepted(new { correlationId });
    }

    // === Consent Management Endpoints ===

    [HttpPost("parties/{partyId}/consent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordConsent(string partyId, [FromBody] RecordConsentRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        string userId = ExtractUserId()!;

        RecordConsent command = new() {
            PartyId = partyId,
            TenantId = tenantId,
            ChannelId = request.ChannelId,
            Purpose = request.Purpose,
            LawfulBasis = request.LawfulBasis,
            ActorUserId = userId,
        };

        CommandProcessingResult result = await SubmitInternalCommandAsync(
            tenantId, partyId, nameof(RecordConsent), command, correlationId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted) {
            return CreateDomainRejectionProblemDetails(result.ErrorMessage, correlationId, tenantId);
        }

        return Ok(new { correlationId = result.CorrelationId ?? correlationId });
    }

    [HttpDelete("parties/{partyId}/consent/{consentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeConsent(string partyId, string consentId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(consentId)) {
            return Problem(title: "Invalid request", detail: "consentId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        string userId = ExtractUserId()!;

        Contracts.Commands.RevokeConsent command = new() {
            PartyId = partyId,
            TenantId = tenantId,
            ConsentId = consentId,
            ActorUserId = userId,
        };

        CommandProcessingResult result = await SubmitInternalCommandAsync(
            tenantId, partyId, nameof(Contracts.Commands.RevokeConsent), command, correlationId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted) {
            return CreateDomainRejectionProblemDetails(result.ErrorMessage, correlationId, tenantId);
        }

        return Ok(new { correlationId = result.CorrelationId ?? correlationId });
    }

    [HttpGet("parties/{partyId}/consent")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsentRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetConsent(string partyId) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        PartyDetail? detail = await GetPartyDetailAsync(tenantId, partyId).ConfigureAwait(false);
        if (detail is null) {
            return NotFound(new ProblemDetails {
                Status = StatusCodes.Status404NotFound,
                Title = "Party not found",
                Detail = $"Party '{partyId}' not found.",
            });
        }

        return Ok(detail.ConsentRecords);
    }

    // === Restriction Endpoints ===

    [HttpPost("parties/{partyId}/restrict")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RestrictProcessing(string partyId, [FromBody] RestrictProcessingRequest? request, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        string userId = ExtractUserId()!;

        Contracts.Commands.RestrictProcessing command = new() {
            PartyId = partyId,
            TenantId = tenantId,
            Reason = request?.Reason,
        };

        CommandProcessingResult result = await SubmitInternalCommandAsync(
            tenantId, partyId, nameof(Contracts.Commands.RestrictProcessing), command, correlationId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted) {
            return CreateDomainRejectionProblemDetails(result.ErrorMessage, correlationId, tenantId);
        }

        return Ok(new { correlationId = result.CorrelationId ?? correlationId });
    }

    [HttpPost("parties/{partyId}/lift-restriction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LiftRestriction(string partyId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        string userId = ExtractUserId()!;

        Contracts.Commands.LiftRestriction command = new() {
            PartyId = partyId,
            TenantId = tenantId,
        };

        CommandProcessingResult result = await SubmitInternalCommandAsync(
            tenantId, partyId, nameof(Contracts.Commands.LiftRestriction), command, correlationId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted) {
            return CreateDomainRejectionProblemDetails(result.ErrorMessage, correlationId, tenantId);
        }

        return Ok(new { correlationId = result.CorrelationId ?? correlationId });
    }

    // === Portability Export Endpoint ===

    [HttpGet("parties/{partyId}/export")]
    [ProducesResponseType(typeof(PartyExportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportPartyData(string partyId) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        PartyErasureStatusRecord? erasureStatus = await erasureRecordStore
            .GetStatusAsync(tenantId, partyId, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (string.Equals(erasureStatus?.Status, ErasureStatus.Erased.ToString(), StringComparison.Ordinal)) {
            return Problem(
                title: "Party erased",
                detail: "This party has been fully erased. Data is no longer available.",
                statusCode: StatusCodes.Status410Gone);
        }

        if (erasureStatus is not null
            && erasureStatus.Status != ErasureStatus.Active.ToString()
            && erasureStatus.Status != "unknown") {
            return Problem(
                title: "Erasure in progress",
                detail: "Data portability export is not available while erasure is in progress.",
                statusCode: StatusCodes.Status409Conflict);
        }

        PartyDetail? detail = await GetPartyDetailAsync(tenantId, partyId).ConfigureAwait(false);
        if (detail is null) {
            return NotFound(new ProblemDetails {
                Status = StatusCodes.Status404NotFound,
                Title = "Party not found",
                Detail = $"Party '{partyId}' not found.",
            });
        }

        if (detail.IsErased) {
            return Problem(
                title: "Party erased",
                detail: "This party has been fully erased. Data is no longer available.",
                statusCode: StatusCodes.Status410Gone);
        }

        logger.LogInformation(
            "Party data exported. TenantId={TenantId}, PartyId={PartyId}",
            tenantId, partyId);

        return Ok(new PartyExportResponse {
            ExportedAt = DateTimeOffset.UtcNow,
            PartyId = detail.Id,
            PartyType = detail.Type.ToString(),
            DisplayName = detail.DisplayName,
            PersonDetails = detail.PersonDetails,
            OrganizationDetails = detail.OrganizationDetails,
            ContactChannels = detail.ContactChannels,
            Identifiers = detail.Identifiers,
            ConsentRecords = detail.ConsentRecords,
        });
    }

    [HttpGet("parties/{partyId}/processing-records")]
    [ProducesResponseType(typeof(ProcessingRecordsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProcessingRecords(string partyId, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(partyId)) {
            return Problem(title: "Invalid request", detail: "partyId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        (IActionResult? accessDenied, string tenantId) = await AuthorizeAdminAccessAndExtractTenantAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (accessDenied is not null) {
            return accessDenied;
        }

        PartyErasureStatusRecord? erasureStatus = await erasureRecordStore
            .GetStatusAsync(tenantId, partyId, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(erasureStatus?.Status, ErasureStatus.Erased.ToString(), StringComparison.Ordinal)) {
            return Problem(
                title: "Party erased",
                detail: "Processing records are no longer queryable for a fully erased party.",
                statusCode: StatusCodes.Status410Gone);
        }

        IReadOnlyList<ProcessingActivityRecord> records = await rebuildService
            .GetProcessingRecordsAsync(tenantId, partyId, cancellationToken)
            .ConfigureAwait(false);

        if (records.Count == 0) {
            PartyDetail? detail = await GetPartyDetailAsync(tenantId, partyId).ConfigureAwait(false);
            if (detail is null) {
                return NotFound(new ProblemDetails {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Party not found",
                    Detail = $"Party '{partyId}' not found.",
                });
            }
        }

        return Ok(new ProcessingRecordsResponse {
            PartyId = partyId,
            TenantId = tenantId,
            Records = records,
        });
    }

    private async Task<PartyDetail?> GetPartyDetailAsync(string tenantId, string partyId) {
        var actorId = new ActorId($"{tenantId}:party-detail:{partyId}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        Task<string?>? jsonTask = null;
        try {
            jsonTask = proxy.GetDetailJsonAsync();
        }
        catch (NotImplementedException) {
            // Older test doubles and actor implementations can still use the typed actor method.
        }

        if (jsonTask is not null) {
            string? json = await jsonTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json)
                && !string.Equals(json.Trim(), "{}", StringComparison.Ordinal)
                && !string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase)) {
                return JsonSerializer.Deserialize<PartyDetail>(json, s_jsonOptions);
            }
        }

        Task<byte[]?>? serializedTask = null;
        try {
            serializedTask = proxy.GetSerializedDetailAsync();
        }
        catch (NotImplementedException) {
            // Older test doubles and actor implementations can still use the typed actor method.
        }

        if (serializedTask is not null) {
            byte[]? payload = await serializedTask.ConfigureAwait(false);
            if (payload is { Length: > 0 }
                && !IsEmptyJsonPayload(payload)) {
                return JsonSerializer.Deserialize<PartyDetail>(payload, s_jsonOptions);
            }
        }

        return await proxy.GetDetailAsync().ConfigureAwait(false);
    }

    private static bool IsEmptyJsonPayload(byte[] payload)
        => (payload.Length == 2 && payload[0] == (byte)'{' && payload[1] == (byte)'}')
            || (payload.Length == 4
                && (payload[0] == (byte)'n' || payload[0] == (byte)'N')
                && (payload[1] == (byte)'u' || payload[1] == (byte)'U')
                && (payload[2] == (byte)'l' || payload[2] == (byte)'L')
                && (payload[3] == (byte)'l' || payload[3] == (byte)'L'));

    private Task<CommandProcessingResult> SubmitInternalCommandAsync<TCommand>(
        string tenantId,
        string partyId,
        string commandType,
        TCommand command,
        string correlationId,
        string userId,
        CancellationToken cancellationToken) {
        var submitCommand = new SubmitCommand(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: tenantId,
            Domain: Domain,
            AggregateId: partyId,
            CommandType: commandType,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: correlationId,
            UserId: userId);

        return commandRouter.RouteCommandAsync(submitCommand, cancellationToken);
    }

    private ObjectResult CreateDomainRejectionProblemDetails(string? errorMessage, string correlationId, string tenantId) {
        string rejectionType = errorMessage?.Replace("Domain rejection: ", string.Empty, StringComparison.Ordinal) ?? "Unknown";
        string simpleType = rejectionType.Contains('.', StringComparison.Ordinal)
            ? rejectionType[(rejectionType.LastIndexOf('.') + 1)..]
            : rejectionType;

        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Domain Rejection",
            Type = $"urn:hexalith:parties:rejection:{simpleType}",
            Detail = errorMessage ?? "The command was rejected by domain logic.",
            Instance = HttpContext.Request.Path,
            Extensions = {
                ["correlationId"] = correlationId,
                ["tenantId"] = tenantId,
                ["correctiveAction"] = "Adjust the request to satisfy domain rules and retry.",
            },
        };

        var result = new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}

public sealed record KeyVersionsResponse {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required IReadOnlyList<int> Versions { get; init; }
}

public sealed record ErasureStatusResponse {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public DateTimeOffset? ErasedAt { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<ErasureVerificationStoreResult>? StoreResults { get; init; }
}

public sealed record ErasureCertificateResponse {
    public required ErasureCertificate Certificate { get; init; }

    public ErasureVerificationReport? VerificationReport { get; init; }
}

public sealed record RebuildProjectionsRequest {
    [Required]
    public string? TenantId { get; init; }

    [Required]
    public string Projection { get; init; } = "all";

    public string? PartyId { get; init; }
}

public sealed record RecordConsentRequest {
    [Required]
    public required string ChannelId { get; init; }

    [Required]
    public required string Purpose { get; init; }

    [Required]
    public required LawfulBasis LawfulBasis { get; init; }
}

public sealed record RestrictProcessingRequest {
    public string? Reason { get; init; }
}

public sealed record PartyExportResponse {
    public required DateTimeOffset ExportedAt { get; init; }

    public required string PartyId { get; init; }

    public required string PartyType { get; init; }

    public string? DisplayName { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }

    public IReadOnlyList<ContactChannel> ContactChannels { get; init; } = [];

    public IReadOnlyList<PartyIdentifier> Identifiers { get; init; } = [];

    public IReadOnlyList<ConsentRecord> ConsentRecords { get; init; } = [];
}

public sealed record ProcessingRecordsResponse {
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public IReadOnlyList<ProcessingActivityRecord> Records { get; init; } = [];
}
