using Microsoft.AspNetCore.Mvc.Testing;

namespace Hexalith.Parties.Sample.Tests;

[CollectionDefinition("PartyEventHandler")]
public sealed class PartyEventHandlerCollection : ICollectionFixture<WebApplicationFactory<Program>>;
