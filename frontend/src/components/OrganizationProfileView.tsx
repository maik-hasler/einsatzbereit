import type { ReactNode } from "react";

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
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75"
									/>
								</svg>
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
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M2.25 6.75c0 8.284 6.716 15 15 15h2.25a2.25 2.25 0 0 0 2.25-2.25v-1.372c0-.516-.351-.966-.852-1.091l-4.423-1.106c-.44-.11-.902.055-1.173.417l-.97 1.293c-.282.376-.769.542-1.21.38a12.035 12.035 0 0 1-7.143-7.143c-.162-.441.004-.928.38-1.21l1.293-.97c.363-.271.527-.734.417-1.173L6.963 3.102a1.125 1.125 0 0 0-1.091-.852H4.5A2.25 2.25 0 0 0 2.25 4.5v2.25Z"
									/>
								</svg>
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
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
									/>
								</svg>
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
								<svg
									className="h-4 w-4 shrink-0 text-gray-400"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
									/>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
									/>
								</svg>
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
