import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import L, { type LatLngBoundsExpression, type PopupEvent } from "leaflet";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import { Link } from "react-router";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { runtimeConfig } from "../../lib/runtimeConfig";
import { createBrandMarkerIcon } from "../../lib/mapMarkerIcon";
import { useOnlineStatus } from "../../hooks/useOnlineStatus";
import EmptyState from "../EmptyState";
import Skeleton from "../Skeleton";
import LoadMoreError from "../LoadMoreError";
import RouteState from "../RouteState";
import { MAP_PAGE_SIZE } from "./useVolunteerOpportunitiesMapData";
// Same Leaflet base + brand overrides SingleMarkerMap.tsx uses - see the
// comment atop SingleMarkerMap.css for why this stays one shared file.
import "../SingleMarkerMap.css";

// Proxied through the backend rather than tile.openstreetmap.org directly so
// visitor IP addresses aren't disclosed to the OpenStreetMap Foundation - see
// docs/ADRs/5_map_and_geocoding_request_proxying.adoc.
const TILE_URL = `${runtimeConfig.apiUrl}/v1/maps/tiles/{z}/{x}/{y}.png`;
const ATTRIBUTION =
	'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

export default function OpportunityResultsMap({
	loading,
	error,
	pins,
	truncated,
	hasFilters,
	onClearFilters,
	onRetry,
}: {
	loading: boolean;
	error: string | null;
	pins: VolunteerOpportunitySummary[];
	truncated: boolean;
	hasFilters: boolean;
	onClearFilters: () => void;
	onRetry: () => void;
}) {
	const { t } = useTranslation();
	const online = useOnlineStatus();
	// See SingleMarkerMap.tsx's identical comment: built lazily so the
	// getComputedStyle read inside createBrandMarkerIcon() happens after mount.
	const brandMarker = useMemo(() => createBrandMarkerIcon(), []);

	const bounds: LatLngBoundsExpression = useMemo(
		() => pins.map((pin) => [pin.latitude as number, pin.longitude as number]),
		[pins],
	);

	// With up to MAP_PAGE_SIZE markers, Leaflet's fixed pane order
	// (markerPane, then a single shared popupPane after every marker) puts a
	// just-opened popup's DOM node after every *other* marker, not next to the
	// one that opened it - tabbing past marker N would visit markers N+1..end
	// before ever reaching this popup's "View details" link. Move focus into
	// the popup as soon as it opens instead of relying on tab order to get
	// there - the same "focus moves into revealed content" convention Modal.tsx
	// already uses for dialogs (frontend/AGENTS.md's Design System table).
	// SingleMarkerMap.tsx doesn't need this: with only one marker, there's
	// nothing after it for tab order to jump through first.
	const focusOpenPopup = useCallback((e: PopupEvent) => {
		e.popup.getElement()?.querySelector("a")?.focus();
	}, []);

	if (loading) {
		return (
			<div role="status" aria-label={t("opportunities.loading")}>
				<Skeleton className="h-96 w-full" />
			</div>
		);
	}

	if (error) {
		return online ? (
			<LoadMoreError
				message={t("opportunities.error", { message: error })}
				retrying={loading}
				onRetry={onRetry}
				data-testid="opportunities-map-error"
			/>
		) : (
			<RouteState
				inline
				variant="offline"
				title={t("routeState.offline.title")}
				message={t("opportunities.offline")}
				data-testid="opportunities-map-offline"
			/>
		);
	}

	if (pins.length === 0) {
		return (
			<EmptyState
				title={t("opportunities.noOnSiteResults")}
				message={
					hasFilters ? t("opportunities.noResultsWithFilters") : undefined
				}
				action={
					hasFilters
						? {
								label: t("opportunities.clearFilters"),
								onClick: onClearFilters,
							}
						: undefined
				}
			/>
		);
	}

	return (
		<div>
			{truncated && (
				<p
					role="status"
					className="mb-2 text-center text-sm text-gray-600"
					data-testid="opportunities-map-truncated"
				>
					{t("opportunities.mapResultsTruncated", { count: MAP_PAGE_SIZE })}
				</p>
			)}
			<div
				data-testid="opportunities-map"
				className="isolate h-96 w-full overflow-hidden rounded-card border border-gray-200 shadow-resting"
			>
				<MapContainer
					bounds={bounds}
					boundsOptions={{ padding: [32, 32], maxZoom: 15 }}
					scrollWheelZoom={false}
					// Same mobile touch-scroll fix as SingleMarkerMap.tsx (#1664) -
					// leaving dragging on unconditionally would make Leaflet claim
					// every touch gesture starting on the map, blocking the page's
					// own vertical swipe-to-scroll.
					dragging={!L.Browser.mobile}
					className="h-full w-full"
				>
					<TileLayer attribution={ATTRIBUTION} url={TILE_URL} />
					{pins.map((pin) => (
						<Marker
							key={pin.id}
							position={[pin.latitude as number, pin.longitude as number]}
							icon={brandMarker}
							// Accessible name for the marker's focus stop - see
							// SingleMarkerMap.tsx's identical comment (#1681).
							title={pin.title}
							eventHandlers={{ popupopen: focusOpenPopup }}
						>
							<Popup>
								<div className="px-3 py-2.5 pr-6 text-sm">
									<p className="font-medium text-gray-800">{pin.title}</p>
									{pin.city && <p className="text-gray-500">{pin.city}</p>}
									<Link
										to={`/volunteer-opportunities/${pin.id}`}
										className="mt-1 inline-block font-medium text-brand-700 underline-offset-2 hover:underline"
									>
										{t("opportunities.viewDetails")}
									</Link>
								</div>
							</Popup>
						</Marker>
					))}
				</MapContainer>
			</div>
		</div>
	);
}
