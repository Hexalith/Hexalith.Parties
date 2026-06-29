namespace Hexalith.Parties.Contracts.Authorization;

public static class PartiesRoles
{
    public const string AdminPolicy = "Admin";

    public const string ConsumerPolicy = "Consumer";

    public const string Admin = "Admin";

    public const string AdminLower = "admin";

    public const string Administrator = "Administrator";

    public const string AdministratorLower = "administrator";

    public const string Consumer = "Consumer";

    public const string ConsumerLower = "consumer";

    public const string TenantOwner = "TenantOwner";

    public const string TenantOwnerLower = "tenantowner";

    public static readonly string[] AdminRoleNames =
    [
        Admin,
        AdminLower,
        Administrator,
        AdministratorLower,
    ];

    public static readonly string[] ConsumerRoleNames =
    [
        Consumer,
        ConsumerLower,
    ];

    public static readonly string[] TenantOwnerRoleNames =
    [
        TenantOwner,
        TenantOwnerLower,
    ];
}
