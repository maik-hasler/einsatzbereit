import L from "leaflet";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import markerIcon2x from "leaflet/dist/images/marker-icon-2x.png?url";
import markerIcon from "leaflet/dist/images/marker-icon.png?url";
import markerShadow from "leaflet/dist/images/marker-shadow.png?url";

L.Icon.Default.mergeOptions({
	iconRetinaUrl: markerIcon2x,
	iconUrl: markerIcon,
	shadowUrl: markerShadow,
});

const TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const ATTRIBUTION =
	'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

interface Props {
	latitude: number;
	longitude: number;
	label: string;
}

export default function SingleMarkerMap({ latitude, longitude, label }: Props) {
	return (
		<div className="h-64 w-full overflow-hidden rounded border">
			<MapContainer
				center={[latitude, longitude]}
				zoom={14}
				scrollWheelZoom={false}
				className="h-full w-full"
			>
				<TileLayer attribution={ATTRIBUTION} url={TILE_URL} />
				<Marker position={[latitude, longitude]}>
					<Popup>
						<span>{label}</span>
					</Popup>
				</Marker>
			</MapContainer>
		</div>
	);
}
