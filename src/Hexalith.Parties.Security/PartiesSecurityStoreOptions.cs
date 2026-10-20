namespace Hexalith.Parties.Security;

/// <summary>Names the existing Dapr component used for security records.</summary>
public sealed class PartiesSecurityStoreOptions
{
    /// <summary>Gets the configuration section for security storage.</summary>
    public const string SectionName = "Parties:Security";

    /// <summary>Gets or sets the state store without changing security record keys or formats.</summary>
    public string StateStoreName { get; set; } = "statestore";
}
