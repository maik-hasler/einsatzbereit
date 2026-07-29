import L from "leaflet";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import { runtimeConfig } from "../lib/runtimeConfig";
// Colocated with this component (not styles/global.css) - see #1383 and the
// comment atop SingleMarkerMap.css for why the overrides moved here too, and
// why this doesn't split into its own CSS chunk (cssCodeSplit is off).
import "./SingleMarkerMap.css";

// Proxied through the backend rather than tile.openstreetmap.org directly so
// visitor IP addresses aren't disclosed to the OpenStreetMap Foundation - see
// docs/ADRs/5_map_and_geocoding_request_proxying.adoc.
const TILE_URL = `${runtimeConfig.apiUrl}/v1/maps/tiles/{z}/{x}/{y}.png`;
const ATTRIBUTION =
	'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

const brandMarker = L.divIcon({
	html: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 28 36" width="28" height="36" style="display:block"><path d="M14 1C7.9 1 3 5.9 3 12c0 8.5 11 23 11 23S25 20.5 25 12C25 5.9 20.1 1 14 1z" fill="#2d8a5e" stroke="white" stroke-width="1.5"/><circle cx="14" cy="12" r="5" fill="white"/></svg>',
	className: "",
	iconSize: [28, 36],
	iconAnchor: [14, 36],
	popupAnchor: [0, -38],
});

interface Props {
	latitude: number;
	longitude: number;
	label: string;
}

export default function SingleMarkerMap({ latitude, longitude, label }: Props) {
	return (
		<div className="isolate h-64 w-full overflow-hidden rounded-xl border border-gray-200 shadow-sm">
			<MapContainer
				center={[latitude, longitude]}
				zoom={14}
				scrollWheelZoom={false}
				className="h-full w-full"
			>
				<TileLayer attribution={ATTRIBUTION} url={TILE_URL} />
				<Marker position={[latitude, longitude]} icon={brandMarker}>
					<Popup>
						<div className="px-3 py-2.5 pr-6 text-sm font-medium text-gray-800">
							{label}
						</div>
					</Popup>
				</Marker>
			</MapContainer>
		</div>
	);
}
