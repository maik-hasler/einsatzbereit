import type { ReactNode } from "react";
import { EnvelopeIcon, GlobeIcon, MapPinIcon, PhoneIcon } from "./icons";
import { cardClass } from "../lib/surfaceClasses";
import { getInitials } from "../lib/initials";
import { pageTitleClass } from "../lib/headingClasses";

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
	const useSidebar = layout === "sidebar" && hasContactInfo;

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
						{logoUrl ? (
							<img
								src={logoUrl}
								alt=""
								width={64}
								height={64}
								className="h-16 w-16 rounded-full object-cover"
							/>
						) : (
							<span className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
								{getInitials(name)}
							</span>
						)}
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

				<div
					className={
						useSidebar
							? "grid gap-8 lg:grid-cols-[minmax(0,1fr)_18rem] lg:gap-12"
							: ""
					}
				>
					<div className="min-w-0">
						{description && (
							<p className="mb-6 max-w-2xl leading-relaxed text-gray-700">
								{description}
							</p>
						)}
						{!useSidebar && hasContactInfo && (
							<div className="mb-6 max-w-md">{contactCard}</div>
						)}
						{children}
					</div>

					{useSidebar && <aside className="self-start">{contactCard}</aside>}
				</div>
			</div>
		</>
	);
}
