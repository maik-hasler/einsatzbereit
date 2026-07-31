using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Primitives;

namespace Infrastructure.Persistence.Outbox;

// Domain events carry Guid-backed value-object IDs (VolunteerOpportunityId, UserId, ...) -
// readonly record structs with a private constructor and a get-only Value property (see e.g.
// Domain.VolunteerOpportunities.VolunteerOpportunityId). System.Text.Json's default
// reflection-based (de)serializer has no public constructor or settable member to populate on
// deserialize, so OutboxMessage.ToDomainEvent() was silently producing a Guid.Empty-backed
// instance instead of throwing - only surfaced by einsatzbereit#1038's cascade-cancel tests,
// the first outbox-dispatched handler to actually look an entity up by the deserialized id
// rather than just logging it. This converter round-trips them as a plain JSON Guid string.
internal sealed class ValueObjectIdJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsValueType
		&& typeof(IValueObject).IsAssignableFrom(typeToConvert)
		&& typeToConvert.GetProperty("Value")?.PropertyType == typeof(Guid)
		&& FindGuidConstructor(typeToConvert) is not null;

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(ValueObjectIdJsonConverter<>).MakeGenericType(typeToConvert))!;

	internal static ConstructorInfo? FindGuidConstructor(Type type) =>
		type.GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
			binder: null,
			types: [typeof(Guid)],
			modifiers: null);

	private sealed class ValueObjectIdJsonConverter<T> : JsonConverter<T>
		where T : struct, IValueObject
	{
		private static readonly ConstructorInfo Constructor =
			FindGuidConstructor(typeof(T))
			?? throw new InvalidOperationException($"{typeof(T)} has no single-Guid constructor.");

		private static readonly PropertyInfo ValueProperty =
			typeof(T).GetProperty("Value")
			?? throw new InvalidOperationException($"{typeof(T)} has no Value property.");

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			(T)Constructor.Invoke([reader.GetGuid()]);

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
			writer.WriteStringValue((Guid)ValueProperty.GetValue(value)!);
	}
}
