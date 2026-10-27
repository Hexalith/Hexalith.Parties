// <copyright file="NaturalLanguageEmbeddingRetryInput.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Memories.Server.Workflows.Contracts;

/// <summary>Story 9.2 Task 8.3 — input to <c>NaturalLanguageEmbeddingRetryWorkflow</c>. Projects the
/// bounded payload-by-value carried in <see cref="FailedNaturalLanguageEmbeddingRecord"/> plus the
/// identifiers needed by the indexing activities.</summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="MemoryUnitId">The memory unit identifier.</param>
/// <param name="RawJsonPayload">The raw JSON payload (already truncated at enqueue time).</param>
/// <param name="EventType">The CloudEvents <c>type</c> attribute.</param>
/// <param name="AggregateType">Optional aggregate-type metadata value.</param>
/// <param name="CaseId">The case identifier.</param>
/// <param name="EmbeddingProvider">The embedding provider identifier.</param>
/// <param name="EmbeddingModel">The embedding model identifier.</param>
/// <param name="EmbeddingDimensions">The embedding dimensions.</param>
public sealed record NaturalLanguageEmbeddingRetryInput(
    string TenantId,
    string MemoryUnitId,
    string RawJsonPayload,
    string EventType,
    string? AggregateType,
    string CaseId,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingDimensions);
