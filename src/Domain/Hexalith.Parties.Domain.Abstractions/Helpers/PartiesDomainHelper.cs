namespace Hexalith.Parties.Domain.Helpers;

using Hexalith.Domain.Aggregates;

/// <summary>
/// Inventory helper.
/// </summary>
public static class PartiesDomainHelper
{
    /// <summary>
    /// Gets the aggregate name for the Customer.
    /// </summary>
    public static string CustomerAggregateName => "Customer";

    /// <summary>
    /// Gets the identifier separator.
    /// </summary>
    /// <value>The identifier separator.</value>
    public static char IdSeparator => '-';

    /// <summary>
    /// Gets the aggregate ID for the Customer.
    /// </summary>
    /// <param name="partitionId">The partition ID.</param>
    /// <param name="companyId">The company ID.</param>
    /// <param name="originId">The origin ID.</param>
    /// <param name="id">The ID.</param>
    /// <returns>The aggregate ID.</returns>
    public static string GetCustomerAggregateId(string partitionId, string companyId, string originId, string id)
        => Aggregate.Normalize(CustomerAggregateName + IdSeparator + partitionId + IdSeparator + companyId + IdSeparator + originId + IdSeparator + id);
}