﻿// ***********************************************************************
// Assembly         : Hexalith.Infrastructure.WebApis.PartiesEvents
// Author           : Jérôme Piquot
// Created          : 11-12-2023
//
// Last Modified By : Jérôme Piquot
// Last Modified On : 11-12-2023
// ***********************************************************************
// <copyright file="CustomerRegisteredProjectionUpdateHandler.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace Hexalith.Infrastructure.WebApis.PartiesEvents.Projections;

using Hexalith.Domain.Aggregates;
using Hexalith.Domain.Events;
using Hexalith.Infrastructure.DaprRuntime.Projections;

using Microsoft.Extensions.Logging;

/// <summary>
/// Class CustomerRegisteredProjectionUpdateHandler.
/// Implements the <see cref="Application.Events.IntegrationEventProjectionUpdateHandler{CustomerRegistered}" />.
/// </summary>
/// <seealso cref="Application.Events.IntegrationEventProjectionUpdateHandler{CustomerRegistered}" />
/// <remarks>
/// Initializes a new instance of the <see cref="CustomerRegisteredProjectionUpdateHandler"/> class.
/// </remarks>
/// <param name="factory">The factory.</param>
/// <param name="logger">The logger.</param>
public class CustomerRegisteredProjectionUpdateHandler(IActorProjectionFactory<Customer> factory, ILogger<CustomerRegisteredProjectionUpdateHandler> logger) : CustomerProjectionUpdateHandler<CustomerRegistered>(factory, logger)
{
}