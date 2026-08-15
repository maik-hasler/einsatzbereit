using Application.Common.Geocoding;
using Application.Common.Messaging;
using Domain.Common;

namespace Application.Maps.GeocodeAddress.v1;

public sealed record GeocodeAddressQuery(Address Address) : IQuery<GeocodingResult>;
