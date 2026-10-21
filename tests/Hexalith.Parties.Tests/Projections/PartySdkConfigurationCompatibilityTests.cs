using System.Reflection;

using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Queries;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class PartySdkConfigurationCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionOptionsUseCanonicalValuesWithPerPropertyLegacyFallback(bool canonical)
    {
        var values = new Dictionary<string, string?>
        {
            ["EventStore:Projections:ReadModelStateStoreName"] = "legacy-store",
            ["EventStore:Projections:FreshnessAgingSeconds"] = "12",
            ["EventStore:Projections:FreshnessStaleSeconds"] = "90",
        };
        if (canonical)
        {
            values["Parties:Projections:ReadModelStateStoreName"] = "parties-store";
            values["Parties:Projections:FreshnessStaleSeconds"] = "180";
        }

        using ServiceProvider provider = CreateProvider(values);
        PartySdkReadModelOptions options = provider.GetRequiredService<IOptions<PartySdkReadModelOptions>>().Value;

        options.ReadModelStateStoreName.ShouldBe(canonical ? "parties-store" : "legacy-store");
        options.FreshnessAgingSeconds.ShouldBe(12);
        options.FreshnessStaleSeconds.ShouldBe(canonical ? 180 : 90);
    }

    [Theory]
    [InlineData("FreshnessAgingSeconds", "12")]
    [InlineData("FreshnessStaleSeconds", "90")]
    public void ValidCanonicalNumbersOverrideMalformedLegacyNumbersBeforeConversion(string property, string value)
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"EventStore:Projections:{property}"] = "invalid-legacy",
            [$"Parties:Projections:{property}"] = value,
        });
        PartySdkReadModelOptions options = provider.GetRequiredService<IOptions<PartySdkReadModelOptions>>().Value;
        (property == "FreshnessAgingSeconds" ? options.FreshnessAgingSeconds : options.FreshnessStaleSeconds)
            .ShouldBe(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ProjectionOptionsRetainDefaultsWithoutEitherSection()
    {
        using ServiceProvider provider = CreateProvider([]);
        provider.GetRequiredService<IOptions<PartySdkReadModelOptions>>().Value.ShouldBe(new PartySdkReadModelOptions());
    }

    [Theory]
    [InlineData("ReadModelStateStoreName", " ")]
    [InlineData("FreshnessAgingSeconds", "-1")]
    [InlineData("FreshnessStaleSeconds", "5")]
    public void InvalidCanonicalOptionsDoNotFallBackToValidLegacyValues(string property, string value)
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["EventStore:Projections:ReadModelStateStoreName"] = "legacy-store",
            ["EventStore:Projections:FreshnessAgingSeconds"] = "12",
            ["EventStore:Projections:FreshnessStaleSeconds"] = "90",
            [$"Parties:Projections:{property}"] = value,
        });

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PartySdkReadModelOptions>>().Value);
    }

    [Theory]
    [InlineData("PartyDetailProjectionQueryActor", typeof(PartyDetailQueryTypes))]
    [InlineData("PartyIndexProjectionQueryActor", typeof(PartyIndexQueryTypes))]
    public void ObsoleteAliasesKeepEveryPublicConstantAndHistoricalWireRoutingName(string legacyName, Type canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        Type legacy = canonical.Assembly.GetType($"Hexalith.Parties.Queries.{legacyName}", throwOnError: true)!;
        legacy.GetCustomAttribute<ObsoleteAttribute>().ShouldNotBeNull();
        FieldInfo[] fields = legacy.GetFields(BindingFlags.Public | BindingFlags.Static);
        fields.Select(field => field.Name).Order().ShouldBe(canonical.GetFields().Select(field => field.Name).Order());
        foreach (FieldInfo field in fields)
        {
            field.GetRawConstantValue().ShouldBe(canonical.GetField(field.Name)!.GetRawConstantValue());
            string historicalValue = field.Name switch
            {
                "ActorTypeName" => legacyName,
                "ProjectionType" => canonical == typeof(PartyDetailQueryTypes) ? "party-detail" : "party-index",
                "DataPortabilityProjectionType" => "party-data-portability",
                "ListAggregateId" => "parties",
                "PartyDomain" => "party",
                "GetPartyQueryType" => "GetParty",
                "PartyDetailQueryType" => "PartyDetail",
                "ExportPartyDataQueryType" => "ExportPartyData",
                "GetProcessingRecordsQueryType" => "GetProcessingRecords",
                "GetErasureStatusQueryType" => "GetErasureStatus",
                "GetErasureCertificateQueryType" => "GetErasureCertificate",
                "PartyIndexQueryType" => "PartyIndex",
                "PartySearchQueryType" => "PartySearch",
                _ => throw new InvalidOperationException($"Pin the historical wire value for {field.Name}."),
            };
            field.GetRawConstantValue().ShouldBe(historicalValue);
        }

        canonical.GetField("ActorTypeName")!.GetRawConstantValue().ShouldBe(legacyName);
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParties(configuration);
        return services.BuildServiceProvider();
    }
}
