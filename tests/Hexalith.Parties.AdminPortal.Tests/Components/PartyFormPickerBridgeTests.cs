using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

public sealed class PartyFormPickerBridgeTests
{
    [Fact]
    public void PartyFormPickerBridge_ForwardsOnlyBoundedPartySelectedDetailToBlazorForm()
    {
        string script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Hexalith.Parties.AdminPortal",
            "wwwroot",
            "party-form-picker.js"));

        script.ShouldContain("addEventListener('party-selected'");
        script.ShouldContain("'OnRelatedPartySelectedAsync'");
        script.ShouldContain("detail.partyId ?? null");
        script.ShouldContain("detail.partyType ?? null");
        script.ShouldContain("detail.status ?? null");
        script.ShouldNotContain("displayName");
        script.ShouldNotContain("tenant", Case.Insensitive);
        script.ShouldNotContain("token", Case.Insensitive);
        script.ShouldNotContain("query", Case.Insensitive);
        script.ShouldNotContain("problem", Case.Insensitive);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Parties.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Hexalith.Parties.slnx from test output directory.");
    }
}
