import { useMemo } from "react";
import L from "leaflet";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import { runtimeConfig } from "../lib/runtimeConfig";
import { createBrandMarkerIcon } from "../lib/mapMarkerIcon";
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

interface Props {
	latitude: number;
	longitude: number;
	label: string;
}

export default function SingleMarkerMap({ latitude, longitude, label }: Props) {
	// Built lazily (not at module scope) so createBrandMarkerIcon()'s
	// getComputedStyle read happens after the component has actually mounted in
	// the browser, once global.css's @theme tokens are guaranteed to be applied
	// (#1129).
	const brandMarker = useMemo(() => createBrandMarkerIcon(), []);

	return (
		<div className="isolate h-64 w-full overflow-hidden rounded-card border border-gray-200 shadow-resting">
			<MapContainer
				center={[latitude, longitude]}
				zoom={14}
				scrollWheelZoom={false}
				// Mirrors the scrollWheelZoom trap above, for touch: leaving
				// dragging on unconditionally makes Leaflet claim every touch
				// gesture that starts on the map (touch-action: none), which
				// blocks the page's own vertical swipe-to-scroll. Disabling it
				// on mobile drops the container to touch-action: pan-x pan-y so
				// a swipe passes through to the page; pinch-zoom and desktop
				// mouse-drag panning are unaffected (#1664).
				dragging={!L.Browser.mobile}
				className="h-full w-full"
			>
				<TileLayer attribution={ATTRIBUTION} url={TILE_URL} />
				{/* Leaflet gives the marker's role="button" focus stop this as its
				accessible name (icon.title, works on any element - unlike `alt`,
				which Leaflet only applies to <img> icons, never a DivIcon's <div>) -
				without it the marker was an unnamed tab stop (WCAG 4.1.2, #1681). */}
				<Marker
					position={[latitude, longitude]}
					icon={brandMarker}
					title={label}
				>
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
