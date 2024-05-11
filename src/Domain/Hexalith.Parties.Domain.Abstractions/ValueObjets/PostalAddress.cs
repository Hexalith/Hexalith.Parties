﻿// ***********************************************************************
// Assembly         : Hexalith.Domain.Parties
// Author           : Jérôme Piquot
// Created          : 09-12-2023
//
// Last Modified By : Jérôme Piquot
// Last Modified On : 12-20-2023
// ***********************************************************************
// <copyright file="PostalAddress.cs" company="Fiveforty SAS Paris France">
//     Copyright (c) Fiveforty SAS Paris France. All rights reserved.
//     Licensed under the MIT license.
//     See LICENSE file in the project root for full license information.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace Hexalith.Parties.Domain.ValueObjets;

using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

/// <summary>
/// Class PostalAddress.
/// </summary>
[DataContract]
[Serializable]
public class PostalAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PostalAddress"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="streetNumber">The street number.</param>
    /// <param name="street">The street.</param>
    /// <param name="postBox">The post box.</param>
    /// <param name="zipCode">The zip code.</param>
    /// <param name="city">The city.</param>
    /// <param name="countyId">The county identifier.</param>
    /// <param name="stateId">The state identifier.</param>
    /// <param name="stateName">Name of the state.</param>
    /// <param name="countryId">The country identifier.</param>
    /// <param name="countryName">Name of the country.</param>
    /// <param name="countryIso2">The country iso2.</param>
    /// <param name="latitude">The latitude.</param>
    /// <param name="longitude">The longitude.</param>
    /// <param name="placeId">The place identifier.</param>
    /// <param name="formattedAddress">The formatted address.</param>
    public PostalAddress(
            string? name,
            string? description,
            string? streetNumber,
            string? street,
            string? postBox,
            string? zipCode,
            string? city,
            string? countyId,
            string? stateId,
            string? stateName,
            string? countryId,
            string? countryName,
            string? countryIso2,
            double? latitude,
            double? longitude,
            string? placeId,
            string? formattedAddress)
    {
        Name = name;
        Description = description;
        StreetNumber = streetNumber;
        Street = street;
        PostBox = postBox;
        ZipCode = zipCode;
        City = city;
        CountyId = countyId;
        StateId = stateId;
        StateName = stateName;
        CountryId = countryId;
        CountryName = countryName;
        CountryIso2 = countryIso2;
        Longitude = longitude;
        Latitude = latitude;
        PlaceId = placeId;
        FormattedAddress = formattedAddress;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostalAddress" /> class.
    /// </summary>
    public PostalAddress()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostalAddress" /> class.
    /// </summary>
    /// <param name="address">The address.</param>
    public PostalAddress(PostalAddress address)
        : this(
              (address ?? throw new ArgumentNullException(nameof(address))).Name,
              address.Description,
              address.StreetNumber,
              address.Street,
              address.PostBox,
              address.ZipCode,
              address.City,
              address.CountyId,
              address.StateId,
              address.StateName,
              address.CountryId,
              address.CountryName,
              address.CountryIso2,
              address.Latitude,
              address.Longitude,
              address.PlaceId,
              address.FormattedAddress)
    {
    }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    /// <value>The city.</value>
    [DataMember(Order = 7)]
    [JsonPropertyOrder(7)]
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the country identifier.
    /// </summary>
    /// <value>The country identifier.</value>
    [DataMember(Order = 12)]
    [JsonPropertyOrder(12)]
    public string? CountryId { get; set; }

    /// <summary>
    /// Gets or sets the country identifier.
    /// </summary>
    /// <value>The country identifier.</value>
    [DataMember(Order = 13)]
    [JsonPropertyOrder(13)]
    public string? CountryIso2 { get; set; }

    /// <summary>
    /// Gets or sets the country identifier.
    /// </summary>
    /// <value>The country identifier.</value>
    [DataMember(Order = 11)]
    [JsonPropertyOrder(11)]
    public string? CountryName { get; set; }

    /// <summary>
    /// Gets or sets the county identifier.
    /// </summary>
    /// <value>The county identifier.</value>
    [DataMember(Order = 8)]
    [JsonPropertyOrder(8)]
    public string? CountyId { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    [DataMember(Order = 2)]
    [JsonPropertyOrder(2)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the formatted address.
    /// </summary>
    /// <value>The formatted address.</value>
    [DataMember(Order = 17)]
    [JsonPropertyOrder(17)]
    public string? FormattedAddress { get; set; }

    /// <summary>
    /// Gets or sets the latitude.
    /// </summary>
    /// <value>The latitude.</value>
    [DataMember(Order = 14)]
    [JsonPropertyOrder(14)]
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude.
    /// </summary>
    /// <value>The longitude.</value>
    [DataMember(Order = 15)]
    [JsonPropertyOrder(15)]
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    [DataMember(Order = 1)]
    [JsonPropertyOrder(1)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the place identifier.
    /// </summary>
    /// <value>The place identifier.</value>
    [DataMember(Order = 16)]
    [JsonPropertyOrder(16)]
    public string? PlaceId { get; set; }

    /// <summary>
    /// Gets or sets the post box.
    /// </summary>
    /// <value>The post box.</value>
    [DataMember(Order = 5)]
    [JsonPropertyOrder(5)]
    public string? PostBox { get; set; }

    /// <summary>
    /// Gets or sets the state identifier.
    /// </summary>
    /// <value>The state identifier.</value>
    [DataMember(Order = 9)]
    [JsonPropertyOrder(9)]
    public string? StateId { get; set; }

    /// <summary>
    /// Gets or sets the state identifier.
    /// </summary>
    /// <value>The state identifier.</value>
    [DataMember(Order = 10)]
    [JsonPropertyOrder(10)]
    public string? StateName { get; set; }

    /// <summary>
    /// Gets or sets the street.
    /// </summary>
    /// <value>The street.</value>
    [DataMember(Order = 4)]
    [JsonPropertyOrder(4)]
    public string? Street { get; set; }

    /// <summary>
    /// Gets or sets the street number.
    /// </summary>
    /// <value>The street number.</value>
    [DataMember(Order = 3)]
    [JsonPropertyOrder(3)]
    public string? StreetNumber { get; set; }

    /// <summary>
    /// Gets or sets the zip code.
    /// </summary>
    /// <value>The zip code.</value>
    [DataMember(Order = 6)]
    [JsonPropertyOrder(6)]
    public string? ZipCode { get; set; }

    /// <summary>
    /// Ares the same.
    /// </summary>
    /// <param name="a">a.</param>
    /// <param name="b">The b.</param>
    /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
    public static bool AreSame(PostalAddress? a, PostalAddress? b)
    {
        return a is null
            ? b is null
            : a == b ||
                a.Name == b?.Name &&
                a.Description == b?.Description &&
                a.StreetNumber == b?.StreetNumber &&
                a.Street == b?.Street &&
                a.PostBox == b?.PostBox &&
                a.ZipCode == b?.ZipCode &&
                a.City == b?.City &&
                a.CountyId == b?.CountyId &&
                a.StateId == b?.StateId &&
                a.StateName == b?.StateName &&
                a.CountryId == b?.CountryId &&
                a.CountryName == b?.CountryName &&
                a.CountryIso2 == b?.CountryIso2 &&
                a.Latitude == b?.Latitude &&
                a.Longitude == b?.Longitude &&
                a.PlaceId == b?.PlaceId;
    }
}