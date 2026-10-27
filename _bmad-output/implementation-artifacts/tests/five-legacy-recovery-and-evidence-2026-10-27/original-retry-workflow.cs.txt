// <copyright file="NaturalLanguageEmbeddingRetryWorkflow.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Memories.Server.Workflows;

using Dapr.Workflow;

using Hexalith.Memories.Contracts.V1;
using Hexalith.Memories.Server.Activities.Indexing;
using Hexalith.Memories.Server.Activities.Ingestion;
using Hexalith.Memories.Server.NaturalLanguage;

/// <summary>Story 9.2 Task 8.4 — minimal retry orchestration that re-runs the NL description +
/// embedding + index activities for memory units whose first attempt degraded (LLM unavailable).
/// Returns <see cref="NaturalLanguageEmbeddingRetryResult"/> instead of throwing so the hosted
/// service can distinguish retry-soon from dead-letter at a structural level.</summary>
public sealed class NaturalLanguageEmbeddingRetryWorkflow
    : Workflow<NaturalLanguageEmbeddingRetryInput, NaturalLanguageEmbeddingRetryResult>
{
    /// <inheritdoc/>
    public override async Task<NaturalLanguageEmbeddingRetryResult> RunAsync(
        WorkflowContext context,
        NaturalLanguageEmbeddingRetryInput input)
    {
        ConsistencyInput consistencyInput = new(input.MemoryUnitId, input.TenantId);
        bool existsAtRetryStart = await context.CallActivityAsync<bool>(
            nameof(CheckMemoryUnitExistsActivity),
            consistencyInput);
        if (!existsAtRetryStart)
        {
            return new NaturalLanguageEmbeddingRetryResult(Indexed: false, Reason: "memory-unit-deleted-during-retry");
        }

        NaturalLanguageDescriptionResult nlResult;
        try
        {
            nlResult = await context.CallActivityAsync<NaturalLanguageDescriptionResult>(
                nameof(GenerateNaturalLanguageDescriptionActivity),
                new NaturalLanguageDescriptionInput(
                    input.TenantId,
                    input.MemoryUnitId,
                    input.RawJsonPayload,
                    input.EventType,
                    input.AggregateType));
        }
        catch (NaturalLanguageDescriptionUnavailableException)
        {
            return new NaturalLanguageEmbeddingRetryResult(Indexed: false, Reason: "llm-still-unavailable");
        }

        EmbeddingResult nlEmbedding = await context.CallActivityAsync<EmbeddingResult>(
            nameof(GenerateEmbeddingActivity),
            new EmbeddingInput(
                input.TenantId,
                nlResult.Description,
                EmbeddingContentKind.NaturalLanguageDescription));

        bool existsBeforeIndex = await context.CallActivityAsync<bool>(
            nameof(CheckMemoryUnitExistsActivity),
            consistencyInput);
        if (!existsBeforeIndex)
        {
            return new NaturalLanguageEmbeddingRetryResult(Indexed: false, Reason: "memory-unit-deleted-during-retry");
        }

        NaturalLanguageIndexInput indexInput = new()
        {
            MemoryUnitId = input.MemoryUnitId,
            TenantId = input.TenantId,
            CaseId = input.CaseId,
            EmbeddingVector = nlEmbedding.Vector,
            EmbeddingProvider = nlEmbedding.Provider,
            EmbeddingModel = string.IsNullOrWhiteSpace(nlEmbedding.Model) ? input.EmbeddingModel : nlEmbedding.Model,
            EmbeddingDimensions = nlEmbedding.Dimensions,
            NaturalLanguageDescription = nlResult.Description,
            DescriptionConfidence = nlResult.EstimatedConfidence,
            ConfidenceSource = nlResult.ConfidenceSource,
        };

        await context.CallActivityAsync<IndexResult>(
            nameof(IndexNaturalLanguageSemanticActivity),
            indexInput);

        return new NaturalLanguageEmbeddingRetryResult(Indexed: true);
    }
}
