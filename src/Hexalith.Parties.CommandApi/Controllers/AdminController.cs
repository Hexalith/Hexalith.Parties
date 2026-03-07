using System.ComponentModel.DataAnnotations;

using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Projections.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.CommandApi.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/v1/admin")]
public sealed class AdminController(
    IProjectionRebuildService rebuildService,
    IPartyKeyManagementService keyManagementService,
    ILogger<AdminController> logger) : ControllerBase
{
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
        });

        return Accepted(new { correlationId });
    }

    [HttpPost("parties/{partyId}/rotate-key")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult RotateKey(string partyId, [FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            return Problem(
                title: "Invalid request",
                detail: "partyId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Problem(
                title: "Invalid request",
                detail: "tenantId is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        _ = Task.Run(async () =>
        {
            try
            {
                PartyKeyInfo keyInfo = await keyManagementService.RotateKeyAsync(tenantId, partyId, CancellationToken.None).ConfigureAwait(false);

                logger.LogInformation(
                    "Key rotation completed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}, NewVersion={Version}",
                    correlationId,
                    tenantId,
                    partyId,
                    keyInfo.Version);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Key rotation failed. CorrelationId={CorrelationId}, TenantId={TenantId}, PartyId={PartyId}",
                    correlationId,
                    tenantId,
                    partyId);
            }
        });

        return Accepted(new { correlationId });
    }
}

public sealed record RebuildProjectionsRequest
{
    [Required]
    public string? TenantId { get; init; }

    [Required]
    public string Projection { get; init; } = "all";

    public string? PartyId { get; init; }
}
