import { useRef } from "react";
import { useTranslation } from "react-i18next";
import L from "leaflet";
import {
	MapContainer,
	TileLayer,
	Marker,
	Popup,
	useMapEvents,
} from "react-leaflet";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import type { OpportunityBounds } from "../hooks/useOpportunityFilters";

const TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";

const DEFAULT_CENTER: [number, number] = [51.1657, 10.4515];
const DEFAULT_ZOOM = 6;
const BOUNDS_EPSILON = 1e-4;

const brandMarker = L.divIcon({
	html: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 28 36" width="28" height="36" style="display:block">
    <path d="M14 1C7.9 1 3 5.9 3 12c0 8.5 11 23 11 23S25 20.5 25 12C25 5.9 20.1 1 14 1z" fill="#2d8a5e" stroke="white" stroke-width="1.5"/>
    <circle cx="14" cy="12" r="5" fill="white"/>
  </svg>`,
	className: "",
	iconSize: [28, 36],
	iconAnchor: [14, 36],
	popupAnchor: [0, -38],
});

interface Props {
	items: VolunteerOpportunitySummary[];
	bounds: OpportunityBounds | undefined;
	onBoundsChange: (bounds: OpportunityBounds) => void;
}

function boundsDifferMaterially(
	a: OpportunityBounds | undefined,
	b: OpportunityBounds,
): boolean {
	if (!a) return true;
	return (
		Math.abs(a.north - b.north) > BOUNDS_EPSILON ||
		Math.abs(a.south - b.south) > BOUNDS_EPSILON ||
		Math.abs(a.east - b.east) > BOUNDS_EPSILON ||
		Math.abs(a.west - b.west) > BOUNDS_EPSILON
	);
}

function BoundsWatcher({
	onBoundsChange,
}: {
	onBoundsChange: (bounds: OpportunityBounds) => void;
}) {
	const lastSentRef = useRef<OpportunityBounds | undefined>(undefined);

	const map = useMapEvents({
		moveend: () => emit(),
		zoomend: () => emit(),
	});

	function emit() {
		const b = map.getBounds();
		const next: OpportunityBounds = {
			north: b.getNorth(),
			south: b.getSouth(),
			east: b.getEast(),
			west: b.getWest(),
		};
		if (!boundsDifferMaterially(lastSentRef.current, next)) return;
		lastSentRef.current = next;
		onBoundsChange(next);
	}

	return null;
}

export default function OpportunityMap({
	items,
	bounds,
	onBoundsChange,
}: Props) {
	const { t } = useTranslation();
	const attribution = t("map.attribution");

	const initialView = useRef<{ center: [number, number]; zoom: number }>({
		center: bounds
			? [(bounds.north + bounds.south) / 2, (bounds.east + bounds.west) / 2]
			: DEFAULT_CENTER,
		zoom: bounds ? 10 : DEFAULT_ZOOM,
	});

	type PinItem = VolunteerOpportunitySummary & {
		latitude: number;
		longitude: number;
	};
	const pins: PinItem[] = items.filter(
		(item): item is PinItem =>
			!item.isRemote &&
			typeof item.latitude === "number" &&
			typeof item.longitude === "number",
	);

	return (
		<div
			data-testid="opportunity-map"
			className="isolate h-[500px] w-full overflow-hidden rounded-xl border border-gray-200 shadow-sm"
		>
			<MapContainer
				center={initialView.current.center}
				zoom={initialView.current.zoom}
				scrollWheelZoom
				className="h-full w-full"
			>
				<TileLayer attribution={attribution} url={TILE_URL} />
				<BoundsWatcher onBoundsChange={onBoundsChange} />
				{pins.map((item) => (
					<Marker
						key={item.id}
						position={[item.latitude, item.longitude]}
						icon={brandMarker}
					>
						<Popup>
							<div className="p-3 pr-6">
								<p className="text-sm font-semibold leading-snug text-gray-900">
									{item.title}
								</p>
								<p className="mt-0.5 text-xs text-gray-500">
									{item.organizationName}
								</p>
								{item.city && (
									<p className="mt-1 text-xs text-gray-400">{item.city}</p>
								)}
								<a
									href={`/volunteer-opportunities/${item.id}`}
									className="mt-2 block text-xs font-medium text-brand-600 hover:text-brand-700 hover:underline"
								>
									{t("map.popup.viewDetails")} &rarr;
								</a>
							</div>
						</Popup>
					</Marker>
				))}
			</MapContainer>
		</div>
	);
}
