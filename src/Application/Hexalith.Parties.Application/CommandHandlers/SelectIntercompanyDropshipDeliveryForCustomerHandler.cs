﻿// ***********************************************************************
// Assembly         : Hexalith.Application.Parties
// Author           : Jérôme Piquot
// Created          : 08-29-2023
//
// Last Modified By : Jérôme Piquot
// Last Modified On : 08-29-2023
// ***********************************************************************
// <copyright file="SelectIntercompanyDropshipDeliveryForCustomerHandler.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace Hexalith.Parties.Application.CommandHandlers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Application.Commands;
using Hexalith.Domain.Aggregates;
using Hexalith.Domain.Messages;
using Hexalith.Extensions.Errors;
using Hexalith.Parties.Application.Errors;
using Hexalith.Parties.Commands;
using Hexalith.Parties.Domain.Aggregates;
using Hexalith.Parties.Events;

/// <summary>
/// Class SetCustomerIntercompanyDeliveryToIndirectHandler.
/// Implements the <see cref="CommandHandler{Hexalith.Application.Parties.Commands.DeselectIntercompanyDropshipDeliveryForCustomer}" />.
/// </summary>
/// <seealso cref="CommandHandler{Hexalith.Application.Parties.Commands.DeselectIntercompanyDropshipDeliveryForCustomer}" />
public class SelectIntercompanyDropshipDeliveryForCustomerHandler : CommandHandler<SelectIntercompanyDropshipDeliveryForCustomer>
{
    /// <inheritdoc/>
    public override async Task<IEnumerable<BaseMessage>> DoAsync([NotNull] SelectIntercompanyDropshipDeliveryForCustomer command, IAggregate? aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        IntercompanyDropshipDeliveryForCustomerSelected selected = new(
             command.PartitionId,
             command.CompanyId,
             command.OriginId,
             command.Id);

        return aggregate is not Customer customer
           ? throw new ApplicationErrorException(CustomerNotRegisteredError.Create(command.TypeName, command.AggregateId))
           : customer.IntercompanyDropship
           ? await Task.FromResult<IEnumerable<BaseMessage>>([]).ConfigureAwait(false)
           : await Task.FromResult<IEnumerable<BaseMessage>>([selected]).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<BaseMessage>> UndoAsync(SelectIntercompanyDropshipDeliveryForCustomer command, IAggregate? aggregate, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException();
    }
}