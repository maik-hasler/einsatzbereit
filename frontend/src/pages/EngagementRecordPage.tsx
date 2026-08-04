import { useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { EngagementRecordEntry } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { formatDateLong, resolveDateLocale } from "../lib/format";
import Button from "../components/Button";
import EmptyState from "../components/EmptyState";
import ErrorBanner from "../components/ErrorBanner";
import Skeleton from "../components/Skeleton";

function formatHours(hours: number, locale: string): string {
	return hours.toLocaleString(locale, { maximumFractionDigits: 2 });
}

// Deliberately a bare top-level route (see App.tsx), not nested under
// AppLayout - the header/footer chrome would otherwise print alongside the
// record itself. `print:hidden` below hides just the page's own navigation
// (back link, print button), leaving the table as the printed page.
export default function EngagementRecordPage() {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	usePageTitle(t("engagementRecord.title"));

	const [entries, setEntries] = useState<EngagementRecordEntry[] | null>(null);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		let cancelled = false;
		api
			.getMyEngagementRecord()
			.then((data) => {
				if (!cancelled) setEntries(data);
			})
			.catch((err) => {
				if (!cancelled)
					setError(getApiErrorMessage(err, t("engagementRecord.loadError")));
			});
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	const locale = resolveDateLocale(i18n.language);
	const totalHours = (entries ?? []).reduce((sum, e) => sum + e.hours, 0);

	return (
		<main className="mx-auto max-w-3xl px-4 py-8 print:max-w-none print:px-0 print:py-0">
			<div className="mb-6 flex items-center justify-between gap-4 print:hidden">
				<Link to="/profile" className="text-sm text-brand-700 hover:underline">
					<span aria-hidden="true">&larr;</span>{" "}
					{t("engagementRecord.backToProfile")}
				</Link>
				{entries && entries.length > 0 && (
					<Button
						type="button"
						variant="outline"
						size="sm"
						onClick={() => window.print()}
					>
						{t("engagementRecord.printButton")}
					</Button>
				)}
			</div>

			<h1 className="text-2xl font-bold text-gray-900">
				{t("engagementRecord.title")}
			</h1>
			<p className="mt-1 text-sm text-gray-500 print:hidden">
				{t("engagementRecord.subtitle")}
			</p>

			{entries === null && !error && (
				<div className="mt-6 space-y-3" role="status">
					<span className="sr-only">{t("engagementRecord.loading")}</span>
					<Skeleton className="h-10 w-full" />
					<Skeleton className="h-10 w-full" />
					<Skeleton className="h-10 w-full" />
				</div>
			)}

			{error && <ErrorBanner message={error} className="mt-6" />}

			{entries && entries.length === 0 && !error && (
				<div className="mt-6">
					<EmptyState
						title={t("engagementRecord.noEntries")}
						message={t("engagementRecord.noEntriesHint")}
					/>
				</div>
			)}

			{entries && entries.length > 0 && (
				<>
					<div
						className="mt-6 overflow-x-auto"
						role="region"
						aria-label={t("engagementRecord.title")}
						// eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- WCAG 2.1.1: makes the scrollable table reachable via keyboard on browsers without native keyboard-focusable scroll containers
						tabIndex={0}
					>
						<table className="w-full border-collapse text-left text-sm">
							<thead>
								<tr className="border-b border-gray-200 text-xs font-semibold tracking-wider text-gray-600 uppercase">
									<th scope="col" className="py-2 pr-4">
										{t("engagementRecord.columnDate")}
									</th>
									<th scope="col" className="py-2 pr-4">
										{t("engagementRecord.columnOrganization")}
									</th>
									<th scope="col" className="py-2 pr-4">
										{t("engagementRecord.columnOpportunity")}
									</th>
									<th scope="col" className="py-2 text-right">
										{t("engagementRecord.columnHours")}
									</th>
								</tr>
							</thead>
							<tbody>
								{entries.map((entry) => (
									<tr
										key={entry.engagementId}
										className="border-b border-gray-100 print:break-inside-avoid"
									>
										<td className="py-2 pr-4 whitespace-nowrap text-gray-700">
											{formatDateLong(
												entry.startDateTime as unknown as string,
												i18n.language,
											)}
										</td>
										<td className="py-2 pr-4 text-gray-700">
											{entry.organizationName ?? "-"}
										</td>
										<td className="py-2 pr-4 text-gray-700">
											{entry.opportunityTitle ??
												t("myEngagements.deletedOpportunityTitle")}
										</td>
										<td className="py-2 text-right font-medium text-gray-900">
											{formatHours(entry.hours, locale)}
										</td>
									</tr>
								))}
							</tbody>
						</table>
					</div>
					<p className="mt-4 text-right text-sm font-semibold text-gray-900">
						{t("engagementRecord.totalHours", {
							hours: formatHours(totalHours, locale),
						})}
					</p>
				</>
			)}
		</main>
	);
}
