// <copyright file="GeoLocation.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Parties.Domain.ValueObjets;

using System.Runtime.Serialization;

/// <summary>
/// Represents a geographic location with latitude and longitude coordinates.
/// </summary>
[DataContract]
public record GeoLocation(
    /// <summary>
    /// Gets or sets the latitude coordinate of the location.
    /// </summary>
    [property:DataMember(Order = 1)]
    double Latitude,

    /// <summary>
    /// Gets or sets the longitude coordinate of the location.
    /// </summary>
    [property : DataMember(Order = 2)]
    double Longitude)
{
}