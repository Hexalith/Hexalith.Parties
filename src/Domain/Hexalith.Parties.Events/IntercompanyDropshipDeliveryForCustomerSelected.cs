﻿// ***********************************************************************
// Assembly         : Hexalith.Domain.Parties
// Author           : Jérôme Piquot
// Created          : 08-21-2023
//
// Last Modified By : Jérôme Piquot
// Last Modified On : 10-03-2023
// ***********************************************************************
// <copyright file="IntercompanyDropshipDeliveryForCustomerSelected.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace Hexalith.Parties.Events;

using System.Runtime.Serialization;

using Hexalith.Extensions;

/// <summary>
/// Class IntercompanyDropshipDeliveryForCustomerSelected.
/// Implements the <see cref="CustomerEvent" />.
/// </summary>
/// <seealso cref="CustomerEvent" />
[DataContract]
[Serializable]
public class IntercompanyDropshipDeliveryForCustomerSelected : CustomerEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntercompanyDropshipDeliveryForCustomerSelected"/> class.
    /// </summary>
    /// <param name="partitionId">The partition identifier.</param>
    /// <param name="companyId">The company identifier.</param>
    /// <param name="originId">The origin identifier.</param>
    /// <param name="id">The identifier.</param>
    public IntercompanyDropshipDeliveryForCustomerSelected(
        string partitionId,
        string companyId,
        string originId,
        string id)
        : base(partitionId, companyId, originId, id)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntercompanyDropshipDeliveryForCustomerSelected"/> class.
    /// </summary>
    [Obsolete(DefaultLabels.ForSerializationOnly, true)]
    public IntercompanyDropshipDeliveryForCustomerSelected()
    {
    }
}