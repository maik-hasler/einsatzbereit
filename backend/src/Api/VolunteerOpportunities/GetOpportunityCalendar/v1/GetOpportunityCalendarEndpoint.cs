using System.Text;
using Api.Common.Endpoints;
using Api.Common.RateLimiting;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Microsoft.AspNetCore.Mvc;

namespace Api.VolunteerOpportunities.GetOpportunityCalendar.v1;

internal sealed class GetOpportunityCalendarEndpoint : IEndpoint
{
	public void MapEndpoint(IEndpointRouteBuilder app) =>
		app.MapGet(
				"/volunteer-opportunities/{opportunityId:guid}/calendar",
				GetCalendarAsync)
			.WithName("GetOpportunityCalendar")
			.Produces<string>(StatusCodes.Status200OK, "text/calendar")
			.ProducesProblem(StatusCodes.Status404NotFound)
			.AllowAnonymous()
			.RequireRateLimiting(RateLimitingPolicies.Read)
			.MapToApiVersion(1);

	private static async Task<IResult> GetCalendarAsync(
		[FromRoute] Guid opportunityId,
		[FromServices] ISender sender,
		CancellationToken cancellationToken)
	{
		var opportunity = await sender.Send(
			new GetVolunteerOpportunityDetailsQuery(opportunityId), cancellationToken);

		if (opportunity is null)
			return Results.NotFound();

		var ics = BuildICalendar(opportunity);
		return Results.Text(ics, "text/calendar; charset=utf-8");
	}

	private static string BuildICalendar(VolunteerOpportunityDetails o)
	{
		var sb = new StringBuilder();
		sb.AppendLine("BEGIN:VCALENDAR");
		sb.AppendLine("VERSION:2.0");
		sb.AppendLine("PRODID:-//Einsatzbereit//EN");
		sb.AppendLine("CALSCALE:GREGORIAN");
		sb.AppendLine("METHOD:PUBLISH");

		var dtstamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
		var url = $"https://einsatzbereit.maik-hasler.de/volunteer-opportunities/{o.Id}";
		var location = o.IsRemote
			? "Remote"
			: string.Join(
				", ",
				new[] { o.Street, o.HouseNumber, o.ZipCode, o.City }
					.Where(s => !string.IsNullOrWhiteSpace(s)));

		if (o.TimeSlots is { Count: > 0 })
		{
			foreach (var ts in o.TimeSlots)
			{
				sb.AppendLine("BEGIN:VEVENT");
				sb.AppendLine($"UID:{o.Id}-{ts.Id}@einsatzbereit");
				sb.AppendLine($"DTSTAMP:{dtstamp}");
				sb.AppendLine($"DTSTART:{ts.StartDateTime.UtcDateTime:yyyyMMddTHHmmssZ}");
				sb.AppendLine($"DTEND:{ts.EndDateTime.UtcDateTime:yyyyMMddTHHmmssZ}");
				sb.AppendLine($"SUMMARY:{Escape(o.Title)}");
				if (!string.IsNullOrWhiteSpace(o.Description))
					sb.AppendLine($"DESCRIPTION:{Escape(o.Description)}");
				if (!string.IsNullOrWhiteSpace(location))
					sb.AppendLine($"LOCATION:{Escape(location)}");
				sb.AppendLine($"URL:{url}");
				sb.AppendLine("END:VEVENT");
			}
		}
		else
		{
			sb.AppendLine("BEGIN:VEVENT");
			sb.AppendLine($"UID:{o.Id}@einsatzbereit");
			sb.AppendLine($"DTSTAMP:{dtstamp}");
			sb.AppendLine($"DTSTART;VALUE=DATE:{DateTimeOffset.UtcNow:yyyyMMdd}");
			sb.AppendLine($"SUMMARY:{Escape(o.Title)}");
			if (!string.IsNullOrWhiteSpace(o.Description))
				sb.AppendLine($"DESCRIPTION:{Escape(o.Description)}");
			if (!string.IsNullOrWhiteSpace(location))
				sb.AppendLine($"LOCATION:{Escape(location)}");
			sb.AppendLine($"URL:{url}");
			sb.AppendLine("END:VEVENT");
		}

		sb.AppendLine("END:VCALENDAR");
		return sb.ToString();
	}

	private static string Escape(string text) =>
		text
			.Replace("\\", "\\\\")
			.Replace(",", "\\,")
			.Replace(";", "\\;")
			.Replace("\r\n", "\\n")
			.Replace("\n", "\\n");
}
