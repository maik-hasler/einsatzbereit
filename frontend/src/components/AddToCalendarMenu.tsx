import { useState } from "react";
import { useTranslation } from "react-i18next";
import { runtimeConfig } from "../lib/runtimeConfig";
import { useDismissableOverlay } from "../hooks/useDismissableOverlay";
import Button from "./Button";
import { CalendarIcon } from "./icons";

interface AddToCalendarMenuProps {
	engagementId: string;
	title: string;
	description?: string;
	location?: string;
	start: string | Date;
	end: string | Date;
}

function toGoogleDateTime(date: Date): string {
	return date
		.toISOString()
		.replace(/[-:]/g, "")
		.replace(/\.\d{3}Z$/, "Z");
}

/**
 * Small action menu offering direct quick-add links (no file download) for
 * Google Calendar and Outlook.com, plus a webcal subscribe link and a plain
 * .ics download as fallbacks for Apple Calendar / desktop calendar apps.
 */
export default function AddToCalendarMenu({
	engagementId,
	title,
	description,
	location,
	start,
	end,
}: AddToCalendarMenuProps) {
	const { t } = useTranslation();
	const [open, setOpen] = useState(false);
	const rootRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);

	const startDate = new Date(start);
	const endDate = new Date(end);

	const googleUrl = `https://calendar.google.com/calendar/render?${new URLSearchParams(
		{
			action: "TEMPLATE",
			text: title,
			dates: `${toGoogleDateTime(startDate)}/${toGoogleDateTime(endDate)}`,
			...(description ? { details: description } : {}),
			...(location ? { location } : {}),
		},
	).toString()}`;

	const outlookUrl = `https://outlook.live.com/calendar/0/deeplink/compose?${new URLSearchParams(
		{
			path: "/calendar/action/compose",
			rru: "addevent",
			subject: title,
			startdt: startDate.toISOString(),
			enddt: endDate.toISOString(),
			...(description ? { body: description } : {}),
			...(location ? { location } : {}),
		},
	).toString()}`;

	const icsUrl = `${runtimeConfig.apiUrl}/v1/engagements/${engagementId}/calendar`;
	const webcalUrl = icsUrl.replace(/^https?:\/\//, "webcal://");

	return (
		<div className="relative" ref={rootRef}>
			<Button
				type="button"
				variant="outline"
				size="sm"
				onClick={() => setOpen((o) => !o)}
				aria-expanded={open}
			>
				<CalendarIcon className="h-3.5 w-3.5" />
				{t("myEngagements.addToCalendar")}
			</Button>

			{open && (
				<ul className="absolute top-full right-0 z-50 mt-1 w-48 rounded-lg border border-gray-200 bg-white py-1 text-sm shadow-modal">
					<li>
						<a
							href={googleUrl}
							target="_blank"
							rel="noopener noreferrer"
							onClick={() => setOpen(false)}
							className="block px-3 py-2 text-gray-700 hover:bg-gray-50"
						>
							{t("myEngagements.addToCalendarGoogle")}
						</a>
					</li>
					<li>
						<a
							href={outlookUrl}
							target="_blank"
							rel="noopener noreferrer"
							onClick={() => setOpen(false)}
							className="block px-3 py-2 text-gray-700 hover:bg-gray-50"
						>
							{t("myEngagements.addToCalendarOutlook")}
						</a>
					</li>
					<li>
						<a
							href={webcalUrl}
							onClick={() => setOpen(false)}
							className="block px-3 py-2 text-gray-700 hover:bg-gray-50"
						>
							{t("myEngagements.addToCalendarApple")}
						</a>
					</li>
					<li>
						<a
							href={icsUrl}
							onClick={() => setOpen(false)}
							className="block px-3 py-2 text-gray-700 hover:bg-gray-50"
						>
							{t("myEngagements.addToCalendarDownload")}
						</a>
					</li>
				</ul>
			)}
		</div>
	);
}
