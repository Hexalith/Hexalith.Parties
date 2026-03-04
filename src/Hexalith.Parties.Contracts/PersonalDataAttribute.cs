namespace Hexalith.Parties.Contracts;

/// <summary>
/// Marks a property as containing personal data subject to GDPR protections.
/// Used by crypto-shredding infrastructure (v1.1) and log sanitization (MVP).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute { }
