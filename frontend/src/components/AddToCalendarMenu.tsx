import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { runtimeConfig } from "../lib/runtimeConfig";

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
	const rootRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		if (!open) return;
		function handleClick(e: MouseEvent) {
			if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
				setOpen(false);
			}
		}
		document.addEventListener("click", handleClick);
		return () => document.removeEventListener("click", handleClick);
	}, [open]);

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
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				aria-expanded={open}
				className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1 text-xs font-medium text-gray-600 transition-colors hover:bg-gray-50"
			>
				<svg
					className="h-3.5 w-3.5"
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="2"
					stroke="currentColor"
					aria-hidden="true"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5"
					/>
				</svg>
				{t("myEngagements.addToCalendar")}
			</button>

			{open && (
				<ul className="absolute right-0 top-full z-50 mt-1 w-48 rounded-lg border border-gray-200 bg-white py-1 text-sm shadow-lg">
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
