import type { ReactNode } from "react";
import { EnvelopeIcon, GlobeIcon, MapPinIcon, PhoneIcon } from "./icons";

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
	/**
	 * "h1" for a standalone page whose primary heading this name is (the
	 * public profile page); "p" (default) for the org app's own Settings tab,
	 * which deliberately has no page-level heading - the org switcher in the
	 * header already shows the org name.
	 */
	nameAs?: "h1" | "p";
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
}: OrganizationProfileViewProps) {
	const NameTag = nameAs;
	const hasContactInfo = !!(contactEmail || contactPhone || website || address);

	return (
		<>
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
							{name.charAt(0).toUpperCase()}
						</span>
					)}
					<div>
						<NameTag className="text-xl font-bold text-gray-900">
							{name}
						</NameTag>
						{subtitle}
					</div>
				</div>
				{actions}
			</div>

			<div data-content-wrapper className="max-w-2xl">
				{beforeContent}

				{description && (
					<p className="mb-6 leading-relaxed text-gray-600">{description}</p>
				)}

				{hasContactInfo && (
					<div className="mb-6 space-y-2.5 rounded-card border border-gray-100 bg-gray-50 px-4 py-4 text-sm text-gray-700">
						{contactEmail && (
							<div className="flex items-center gap-3">
								<EnvelopeIcon className="h-4 w-4 shrink-0 text-gray-400" />
								<a
									href={`mailto:${contactEmail}`}
									className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
								>
									{contactEmail}
								</a>
							</div>
						)}
						{contactPhone && (
							<div className="flex items-center gap-3">
								<PhoneIcon className="h-4 w-4 shrink-0 text-gray-400" />
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
								<GlobeIcon className="h-4 w-4 shrink-0 text-gray-400" />
								<a
									href={website}
									target="_blank"
									rel="noopener noreferrer"
									className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
								>
									{website}
								</a>
							</div>
						)}
						{address && (
							<div className="flex items-center gap-3">
								<MapPinIcon className="h-4 w-4 shrink-0 text-gray-400" />
								<span>
									{address.street} {address.houseNumber}, {address.zipCode}{" "}
									{address.city}
								</span>
							</div>
						)}
					</div>
				)}

				{children}
			</div>
		</>
	);
}
