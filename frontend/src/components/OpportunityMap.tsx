import { useEffect } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import L from "leaflet";
import {
	MapContainer,
	TileLayer,
	Marker,
	Popup,
	useMapEvents,
} from "react-leaflet";
import markerIcon2x from "leaflet/dist/images/marker-icon-2x.png?url";
import markerIcon from "leaflet/dist/images/marker-icon.png?url";
import markerShadow from "leaflet/dist/images/marker-shadow.png?url";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import type { OpportunityBounds } from "../hooks/useOpportunityFilters";

L.Icon.Default.mergeOptions({
	iconRetinaUrl: markerIcon2x,
	iconUrl: markerIcon,
	shadowUrl: markerShadow,
});

const TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";

const DEFAULT_CENTER: [number, number] = [51.1657, 10.4515];
const DEFAULT_ZOOM = 6;

interface Props {
	items: VolunteerOpportunitySummary[];
	bounds: OpportunityBounds | undefined;
	onBoundsChange: (bounds: OpportunityBounds) => void;
}

function BoundsWatcher({
	onBoundsChange,
}: {
	onBoundsChange: (bounds: OpportunityBounds) => void;
}) {
	const map = useMapEvents({
		moveend: () => emit(),
		zoomend: () => emit(),
	});

	function emit() {
		const b = map.getBounds();
		onBoundsChange({
			north: b.getNorth(),
			south: b.getSouth(),
			east: b.getEast(),
			west: b.getWest(),
		});
	}

	useEffect(() => {
		emit();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	return null;
}

export default function OpportunityMap({
	items,
	bounds,
	onBoundsChange,
}: Props) {
	const { t } = useTranslation();
	const attribution = t("map.attribution");

	const initialCenter: [number, number] = bounds
		? [(bounds.north + bounds.south) / 2, (bounds.east + bounds.west) / 2]
		: DEFAULT_CENTER;
	const initialZoom = bounds ? 10 : DEFAULT_ZOOM;

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
			className="h-[500px] w-full overflow-hidden rounded border"
		>
			<MapContainer
				center={initialCenter}
				zoom={initialZoom}
				scrollWheelZoom
				className="h-full w-full"
			>
				<TileLayer attribution={attribution} url={TILE_URL} />
				<BoundsWatcher onBoundsChange={onBoundsChange} />
				{pins.map((item) => (
					<Marker key={item.id} position={[item.latitude, item.longitude]}>
						<Popup>
							<strong>{item.title}</strong>
							<div className="mt-1 text-xs text-gray-600">
								{item.organizationName}
							</div>
							<Link
								to={`/volunteer-opportunities/${item.id}`}
								className="mt-2 inline-block text-sm text-blue-600 hover:underline"
							>
								{t("map.popup.viewDetails")}
							</Link>
						</Popup>
					</Marker>
				))}
			</MapContainer>
		</div>
	);
}
