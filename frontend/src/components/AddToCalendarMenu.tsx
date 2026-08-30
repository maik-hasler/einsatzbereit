import { useState } from "react";
import { useTranslation } from "react-i18next";
import { runtimeConfig } from "../lib/runtimeConfig";
import { useDismissableOverlay } from "../hooks/useDismissableOverlay";
import { buildIcsEvent, toIcsDataUrl } from "../lib/ics";
import Button from "./Button";
import { CalendarIcon } from "./icons";

interface AddToCalendarMenuProps {
	/**
	 * Engagement whose live server feed the Apple/.ics entries point at. The
	 * public opportunity page has no engagement yet, so it passes `icsUid`
	 * instead and the menu builds the event in the browser (#2330).
	 */
	engagementId?: string;

	/** Identity for the locally built event, so a re-import updates rather
	 *  than duplicates it. Required when `engagementId` is absent. */
	icsUid?: string;

	title: string;
	description?: string;
	location?: string;
	url?: string;
	start: string | Date;
	end: string | Date;

	/** Hidden below sm: where a row of labelled buttons will not fit. */
	labelClassName?: string;
}

function toGoogleDateTime(date: Date): string {
	return date
		.toISOString()
		.replace(/[-:]/g, "")
		.replace(/\.\d{3}Z$/, "Z");
}

export default function AddToCalendarMenu({
	engagementId,
	icsUid,
	title,
	description,
	location,
	url,
	start,
	end,
	labelClassName,
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

	// A webcal:// subscription only means anything for a feed the server keeps
	// updating, so it is offered for an engagement and left out for the
	// one-off event built here.
	const serverIcsUrl = engagementId
		? `${runtimeConfig.apiUrl}/v1/engagements/${engagementId}/calendar`
		: null;
	const webcalUrl = serverIcsUrl?.replace(/^https?:\/\//, "webcal://") ?? null;

	const icsUrl =
		serverIcsUrl ??
		toIcsDataUrl(
			buildIcsEvent({
				uid: icsUid ?? `${startDate.toISOString()}-${title}`,
				title,
				description,
				location,
				url,
				start: startDate,
				end: endDate,
			}),
		);

	return (
		<div className="relative" ref={rootRef}>
			<Button
				type="button"
				variant="outline"
				size="sm"
				onClick={() => setOpen((o) => !o)}
				aria-expanded={open}
				title={t("myEngagements.addToCalendar")}
				aria-label={t("myEngagements.addToCalendar")}
			>
				<CalendarIcon className="h-3.5 w-3.5" />
				<span className={labelClassName}>
					{t("myEngagements.addToCalendar")}
				</span>
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
					{webcalUrl && (
						<li>
							<a
								href={webcalUrl}
								onClick={() => setOpen(false)}
								className="block px-3 py-2 text-gray-700 hover:bg-gray-50"
							>
								{t("myEngagements.addToCalendarApple")}
							</a>
						</li>
					)}
					<li>
						<a
							href={icsUrl}
							{...(serverIcsUrl ? {} : { download: "einsatzbereit.ics" })}
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
