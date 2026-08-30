import type { ReactNode } from "react";
import { EnvelopeIcon, GlobeIcon, MapPinIcon, PhoneIcon } from "./icons";
import { cardClass } from "../lib/surfaceClasses";
import { pageTitleClass } from "../lib/headingClasses";
import OrgAvatar from "./OrgAvatar";

interface OrganizationAddress {
	street: string;
	houseNumber: string;
	zipCode: string;
	city: string;
}

interface OrganizationProfileViewProps {
	name: string;
	logoUrl?: string | null;
	description?: string | null;
	contactEmail?: string | null;
	contactPhone?: string | null;
	website?: string | null;
	address?: OrganizationAddress | null;
	subtitle?: ReactNode;
	actions?: ReactNode;
	beforeContent?: ReactNode;
	children?: ReactNode;

	nameAs?: "h1" | "p";

	layout?: "sidebar" | "stacked";

	showHeader?: boolean;

	centered?: boolean;
}

export default function OrganizationProfileView({
	name,
	logoUrl,
	description,
	contactEmail,
	contactPhone,
	website,
	address,
	subtitle,
	actions,
	beforeContent,
	children,
	nameAs = "p",
	layout = "sidebar",
	showHeader = true,
	centered = false,
}: OrganizationProfileViewProps) {
	const NameTag = nameAs;
	const hasContactInfo = !!(contactEmail || contactPhone || website || address);
	// Deliberately not gated on `hasContactInfo`: dropping the second column for
	// an organization with no contact details made the main column - and with it
	// the "current needs" heading and its empty state - jump between 688px and
	// 1024px between one profile and the next (#2331). The column is reserved
	// either way; only the card inside it is conditional.
	const useSidebar = layout === "sidebar";

	const contactCard = (
		<div className={`space-y-2.5 text-sm text-gray-700 ${cardClass}`}>
			{contactEmail && (
				<div className="flex items-center gap-3">
					<EnvelopeIcon className="h-4 w-4 shrink-0 text-brand-700" />
					<a
						href={`mailto:${contactEmail}`}
						className="min-w-0 break-words text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{contactEmail}
					</a>
				</div>
			)}
			{contactPhone && (
				<div className="flex items-center gap-3">
					<PhoneIcon className="h-4 w-4 shrink-0 text-brand-700" />
					<a
						href={`tel:${contactPhone}`}
						className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{contactPhone}
					</a>
				</div>
			)}
			{website && (
				<div className="flex items-center gap-3">
					<GlobeIcon className="h-4 w-4 shrink-0 text-brand-700" />
					<a
						href={website}
						target="_blank"
						rel="noopener noreferrer"
						className="min-w-0 break-words text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{website}
					</a>
				</div>
			)}
			{address && (
				<div className="flex items-center gap-3">
					<MapPinIcon className="h-4 w-4 shrink-0 text-brand-700" />
					<span>
						{address.street} {address.houseNumber}, {address.zipCode}{" "}
						{address.city}
					</span>
				</div>
			)}
		</div>
	);

	return (
		<>
			{showHeader && (
				<div className="mb-6 flex items-start justify-between gap-4">
					<div className="flex items-center gap-4">
						<OrgAvatar name={name} logoUrl={logoUrl} size="3xl" />
						<div>
							<NameTag
								className={
									nameAs === "h1"
										? `text-gray-900 ${pageTitleClass}`
										: "font-display text-2xl font-bold text-gray-900"
								}
							>
								{name}
							</NameTag>
							{subtitle}
						</div>
					</div>
					{actions}
				</div>
			)}

			<div
				data-content-wrapper
				className={centered ? "mx-auto max-w-5xl" : "max-w-5xl"}
			>
				{beforeContent}

				{/* Above the grid rather than inside its first column, so the
				    single-column mobile layout reads description -> contact ->
				    content instead of pushing the contact card below everything. */}
				{description && (
					<p lang="de" className="mb-6 max-w-2xl leading-relaxed text-gray-700">
						{description}
					</p>
				)}

				<div
					className={
						useSidebar
							? "grid gap-8 lg:grid-cols-[minmax(0,1fr)_18rem] lg:gap-12"
							: ""
					}
				>
					{/* First in the DOM, and explicitly placed into the second column
					    from `lg` up. Stacked after the main column, contact details -
					    most of the reason a visitor opens a profile - landed under the
					    whole opportunity list and the report link, ~3,000px down a mobile
					    page (#2331). Reordering in the DOM rather than with `order` keeps
					    the single-column reading and focus sequence matching what is on
					    screen. */}
					{useSidebar && hasContactInfo && (
						<aside className="self-start lg:col-start-2 lg:row-start-1">
							{contactCard}
						</aside>
					)}

					<div
						className={`min-w-0 ${useSidebar ? "lg:col-start-1 lg:row-start-1" : ""}`}
					>
						{!useSidebar && hasContactInfo && (
							<div className="mb-6 max-w-md">{contactCard}</div>
						)}
						{children}
					</div>
				</div>
			</div>
		</>
	);
}
