using System.Text.Json;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Serialization;

public sealed class PartyIdentityContractTests
{
    [Fact]
    public void VersionedSafeEvidence_RoundTripsWithoutProfileOrLoginAliases()
    {
        var evidence = new PartyIdentityResult(PartyIdentityOutcome.Resolved, new(1, "tenant-a", "party-1", PartyIdentityClassification.Organization,
            true, false, false, 4, DateTimeOffset.UnixEpoch, "observation", null));
        string json = JsonSerializer.Serialize(evidence, PartiesJsonOptions.Default);
        JsonSerializer.Deserialize<PartyIdentityResult>(json, PartiesJsonOptions.Default).ShouldBe(evidence);
        foreach (string forbidden in new[] { "issuer", "subject", "email", "displayName", "personDetails", "bearer" })
        {
            json.ShouldNotContain(forbidden);
        }
    }

    [Fact]
    public void ProvisioningIntent_RoundTripsCanonicalOriginal()
    {
        var identity = new AgentPartyIdentity("tenant-a", "01HX0000000000000000000001", AgentPartyIdMapping.Create("tenant-a", "01HX0000000000000000000001"), 1, "logical", new string('a', 64), DateTimeOffset.UnixEpoch);
        var command = new ProvisionAgentParty(identity);
        JsonSerializer.Deserialize<ProvisionAgentParty>(JsonSerializer.Serialize(command, PartiesJsonOptions.Default), PartiesJsonOptions.Default)!.Identity.ShouldBe(identity);
    }
}
