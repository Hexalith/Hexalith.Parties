using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Components.Shared;

public readonly record struct PartyLifecycleState
{
    private PartyLifecycleState(string value) => Value = value;

    public string Value { get; }

    public static PartyLifecycleState Active { get; } = new(nameof(Active));

    public static PartyLifecycleState Inactive { get; } = new(nameof(Inactive));

    public static PartyLifecycleState Restricted { get; } = new(nameof(Restricted));

    public static PartyLifecycleState Erased { get; } = new(nameof(Erased));

    public static PartyLifecycleState FromBooleans(bool isActive, bool isRestricted, bool isErased)
        => isErased
            ? Erased
            : isRestricted
                ? Restricted
                : FromListRow(isActive, isErased: false);

    public static PartyLifecycleState FromDetail(PartyDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return FromBooleans(detail.IsActive, detail.IsRestricted, detail.IsErased);
    }

    public static PartyLifecycleState FromListRow(bool isActive, bool isErased)
        => isErased
            ? Erased
            : isActive
                ? Active
                : Inactive;

    public override string ToString() => Value;
}
