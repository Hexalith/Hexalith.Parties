using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

public sealed class PartyIdentityAdmissionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("unsupported")]
    [InlineData("binding-closure")]
    public async Task MissingOrUnsupportedPolicy_DeniesBeforeAnyProtectedUnwrap(string? trigger)
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        IPartyIdentityAuthority authority = Substitute.For<IPartyIdentityAuthority>();
        authority.Admit(Arg.Any<CommandEnvelope>()).Returns(new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Bind", "message", "logical", "digest"),
            "writer", "01HX0000000000000000000001", "01HX0000000000000000000001", 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null));
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var processor = new PartyDomainProcessor(protection, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<PartyDomainProcessor>.Instance,
            identityAuthority: authority, identityHistoryCustody: Substitute.For<IIdentityHistoryCustody>(),
            identityOptions: Options.Create(new PartyIdentityOptions { PolicyId = "synthetic", Retention = TimeSpan.FromDays(1), ExpiryTrigger = trigger }));
        var payload = new EstablishHumanActorBinding("tenant-a", "party-1", "01HX0000000000000000000001", 1, 1, 0, DateTimeOffset.UtcNow, "logical", "synthetic");
        var command = new CommandEnvelope("01HX0000000000000000000001", "tenant-a", "party", "party-1", typeof(EstablishHumanActorBinding).FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, PartiesJsonOptions.Default), "correlation", null, "writer", null);
        (await processor.ProcessAsync(command, new DomainServiceCurrentState(new { marker = "protected" }, [], 1, 1), TestContext.Current.CancellationToken)).IsRejection.ShouldBeTrue();
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }
}
