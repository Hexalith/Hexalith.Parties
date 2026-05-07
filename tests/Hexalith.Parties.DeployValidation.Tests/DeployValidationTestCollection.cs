namespace Hexalith.Parties.DeployValidation.Tests;

/// <summary>
/// Serializes the deploy-validation tests so two classes don't race on
/// <c>Path.GetTempPath()</c> + Process.Start of pwsh against overlapping
/// per-test temp directories. Defining the collection makes the
/// <c>[Collection("DeployValidation")]</c> attribute on the test classes
/// resolve to a real definition rather than a dangling label.
/// </summary>
[CollectionDefinition("DeployValidation", DisableParallelization = true)]
public sealed class DeployValidationTestCollection
{
}
