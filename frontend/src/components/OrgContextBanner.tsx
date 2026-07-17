import { Link } from "react-router";
import { useTranslation } from "react-i18next";

export default function OrgContextBanner({
	organizationId,
	organizationName,
}: {
	organizationId: string;
	organizationName: string;
}) {
	const { t } = useTranslation();

	return (
		<Link
			to={`/app/${organizationId}/dashboard`}
			className="mb-4 inline-flex items-center gap-2 rounded-lg border border-brand-100 bg-brand-50 px-3 py-1.5 text-sm font-medium text-brand-700 transition-colors hover:bg-brand-100"
		>
			<svg
				className="h-4 w-4 shrink-0"
				fill="none"
				viewBox="0 0 24 24"
				strokeWidth="1.5"
				stroke="currentColor"
				aria-hidden="true"
			>
				<path
					strokeLinecap="round"
					strokeLinejoin="round"
					d="M2.25 21h19.5m-18-18v18m10.5-18v18m6-13.5V21M6.75 6.75h.75m-.75 3h.75m-.75 3h.75m3-6h.75m-.75 3h.75m-.75 3h.75M6.75 21v-3.375c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21M3 3h12m-.75 4.5H21m-3.75 3H21m-3.75 3H21"
				/>
			</svg>
			<span className="truncate">
				{t("organization.contextBanner", { name: organizationName })}
			</span>
		</Link>
	);
}
