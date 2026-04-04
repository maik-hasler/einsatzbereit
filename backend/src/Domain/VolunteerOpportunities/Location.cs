using System.Text.Json.Serialization;

namespace Domain.VolunteerOpportunities;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PhysicalLocation), "physical")]
[JsonDerivedType(typeof(RemoteLocation), "remote")]
public abstract record Location;
