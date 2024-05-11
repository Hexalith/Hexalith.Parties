// ***********************************************************************
// Assembly         : Hexalith.Domain.Parties
// Author           : Jérôme Piquot
// Created          : 08-21-2023
//
// Last Modified By : Jérôme Piquot
// Last Modified On : 08-29-2023
// ***********************************************************************
// <copyright file="Customer.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>
// <summary></summary>
// ***********************************************************************

/// <summary>
/// The Aggregates namespace.
/// </summary>
namespace Hexalith.Parties.Domain.Aggregates;

using System.Runtime.Serialization;

using Hexalith.Domain.Aggregates;
using Hexalith.Domain.Events;
using Hexalith.Parties.Domain.Helpers;
using Hexalith.Parties.Domain.ValueObjets;
using Hexalith.Parties.Events;

/// <summary>
/// Class Customer.
/// Implements the <see cref="Aggregates.Aggregate" />
/// Implements the <see cref="Aggregates.IAggregate" />
/// Implements the <see cref="IEquatable{Aggregates.Aggregate}" />
/// Implements the <see cref="IEquatable{Customer}" />.
/// </summary>
/// <seealso cref="Aggregates.Aggregate" />
/// <seealso cref="Aggregates.IAggregate" />
/// <seealso cref="IEquatable{Aggregates.Aggregate}" />
/// <seealso cref="IEquatable{Customer}" />
[DataContract]
public record Customer(
    string PartitionId,
    string CompanyId,
    string OriginId,
    string Id,
    [property: DataMember(Order = 5)] string Name,
    [property: DataMember(Order = 6)] PartyType PartyType,
    [property: DataMember(Order = 7)] Contact Contact,
    [property: DataMember(Order = 8)] string? WarehouseId,
    [property: DataMember(Order = 9)] string? CommissionSalesGroupId,
    [property: DataMember(Order = 10)] string? GroupId,
    [property: DataMember(Order = 11)] string? SalesCurrencyId,
    [property: DataMember(Order = 12)] bool IntercompanyDropship,
    [property: DataMember(Order = 13)] DateTimeOffset Date) : EntityAggregate(PartitionId, CompanyId, OriginId, Id)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Customer"/> class.
    /// </summary>
    public Customer()
        : this(
              string.Empty,
              string.Empty,
              string.Empty,
              string.Empty,
              string.Empty,
              PartyType.Other,
              new Contact(),
              null,
              null,
              null,
              null,
              false,
              DateTimeOffset.MinValue)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Customer" /> class.
    /// </summary>
    /// <param name="customer">The customer.</param>
    public Customer(CustomerRegistered customer)
        : this(
              (customer ?? throw new ArgumentNullException(nameof(customer))).PartitionId,
              customer.CompanyId,
              customer.OriginId,
              customer.Id,
              customer.Name,
              customer.PartyType,
              new Contact(customer.Contact),
              customer.WarehouseId,
              customer.CommissionSalesGroupId,
              customer.GroupId,
              customer.SalesCurrencyId,
              false,
              customer.Date)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Customer"/> class.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <param name="intercompanyDropship">if set to <c>true</c> [intercompany dropship].</param>
    public Customer(CustomerRegistered customer, bool intercompanyDropship)
        : this(customer) => IntercompanyDropship = intercompanyDropship;

    /// <inheritdoc/>
    public override (IAggregate Aggregate, IEnumerable<BaseEvent> Events) Apply(BaseEvent domainEvent)
    {
        return domainEvent switch
        {
            CustomerInformationChanged changed => (this with
            {
                Name = changed.Name,
                Contact = new Contact(changed.Contact),
                WarehouseId = changed.WarehouseId,
                CommissionSalesGroupId = changed.CommissionSalesGroupId,
                Date = changed.Date,
                PartyType = changed.PartyType,
                GroupId = changed.GroupId,
                SalesCurrencyId = changed.SalesCurrencyId,
            }, [domainEvent]),
            IntercompanyDropshipDeliveryForCustomerSelected => (this with { IntercompanyDropship = true }, [domainEvent]),
            IntercompanyDropshipDeliveryForCustomerDeselected => (this with { IntercompanyDropship = false }, [domainEvent]),
            CustomerRegistered registered => (new Customer(registered), [domainEvent]),
            _ => base.Apply(domainEvent),
        };
    }

    /// <inheritdoc/>
    public override string AggregateName => PartiesDomainHelper.CustomerAggregateName;

    /// <summary>
    /// Converts to change customer information event.
    /// </summary>
    /// <returns>CustomerInformationChanged.</returns>
    public CustomerInformationChanged ToCustomerInformationChanged()
    {
        return new CustomerInformationChanged(
            PartitionId,
            CompanyId,
            OriginId,
            Id,
            Name,
            PartyType,
            new Contact(Contact),
            WarehouseId,
            CommissionSalesGroupId,
            GroupId,
            SalesCurrencyId,
            Date);
    }

    /// <summary>
    /// Converts to register customer event.
    /// </summary>
    /// <returns>CustomerRegistered.</returns>
    public CustomerRegistered ToCustomerRegistered()
    {
        return new CustomerRegistered(
            PartitionId,
            CompanyId,
            OriginId,
            Id,
            Name,
            PartyType,
            new Contact(Contact),
            WarehouseId,
            CommissionSalesGroupId,
            GroupId,
            SalesCurrencyId,
            Date);
    }

    /// <inheritdoc/>
    public override bool IsInitialized() => !string.IsNullOrWhiteSpace(Id);

    /// <inheritdoc/>
    protected override string DefaultAggregateId()
        => PartiesDomainHelper.GetCustomerAggregateId(PartitionId, CompanyId, OriginId, Id);
}