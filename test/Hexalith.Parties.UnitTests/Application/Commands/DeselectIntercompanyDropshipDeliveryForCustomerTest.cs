﻿// ***********************************************************************
// Assembly         : Hexalith.UnitTests
// Author           : Jérôme Piquot
// Created          : 12-07-2023
//
// Last Modified By : Jérôme Piquot
// Last Modified On : 12-07-2023
// ***********************************************************************
// <copyright file="DeselectIntercompanyDropshipDeliveryForCustomerTest.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace Hexalith.Parties.UnitTests.Application.Commands;

using FluentAssertions;

using Hexalith.Application.Commands;
using Hexalith.Parties.Commands;
using Hexalith.TestMocks;

/// <summary>
/// Class DeselectIntercompanyDropshipDeliveryForCustomerTest.
/// Implements the <see cref="PolymorphicSerializationTestBase{Hexalith.Application.Parties.Commands.DeselectIntercompanyDropshipDeliveryForCustomer, BaseCommand}" />.
/// </summary>
/// <seealso cref="PolymorphicSerializationTestBase{Hexalith.Application.Parties.Commands.DeselectIntercompanyDropshipDeliveryForCustomer, BaseCommand}" />
public class DeselectIntercompanyDropshipDeliveryForCustomerTest : PolymorphicSerializationTestBase<DeselectIntercompanyDropshipDeliveryForCustomer, BaseCommand>
{
    /// <summary>
    /// Defines the test method CustomerRegisteredCheckAggregateId.
    /// </summary>
    [Fact]
    public void CustomerRegisteredCheckAggregateId()
    {
        DeselectIntercompanyDropshipDeliveryForCustomer e = ToSerializeObject() as DeselectIntercompanyDropshipDeliveryForCustomer;
        _ = e.AggregateId.Should().Be("Customer-PART1-Company1-ORIG1-Cust123456");
    }

    /// <summary>
    /// Converts to serialize object.
    /// </summary>
    /// <returns>System.Object.</returns>
    public override object ToSerializeObject() => DummyPartiesApplicationHelper.DummyDeselectIntercompanyDropshipDeliveryForCustomer();
}