// <copyright file="CustomerMaintenanceController.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Parties.CommandsWebApis.Controllers;

using Hexalith.Application.Aggregates;
using Hexalith.Infrastructure.WebApis.Controllers;
using Hexalith.Parties.Domain.Aggregates;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Represents a controller for maintaining customer entities.
/// </summary>
/// <typeparam name="Customer">The type of the customer aggregate.</typeparam>
/// <seealso cref="AggregateMaintenanceController{Customer}" />
[ApiController]
[Route("customers")]
public class CustomerMaintenanceController(IAggregateMaintenance<Customer> aggregateMaintenance)
    : AggregateMaintenanceController<Customer>(aggregateMaintenance)
{
}