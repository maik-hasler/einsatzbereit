import { useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import L from "leaflet";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import { runtimeConfig } from "../lib/runtimeConfig";
import { brandColor } from "../lib/brandColor";

import "./SingleMarkerMap.css";

const TILE_URL = `${runtimeConfig.apiUrl}/v1/maps/tiles/{z}/{x}/{y}.png`;
const ATTRIBUTION =
	'&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

function MapAccessibleName({ label }: { label: string }) {
	const map = useMap();
	const { t } = useTranslation();
	useEffect(() => {
		const container = map.getContainer();
		container.setAttribute("role", "group");
		container.setAttribute(
			"aria-label",
			t("opportunities.mapLabel", { location: label }),
		);
	}, [map, label, t]);
	return null;
}

// Leaflet hard-codes the popup's close control as aria-label="Close popup"
// and, because this map keeps keyboard={false} (a fixed snapshot must not pan
// on arrow keys), never binds Escape to closing it either - so on a German
// page the only way out was announced in English and the site-wide dismissal
// key did nothing (#2328). Both are restored here without handing arrow keys
// back to Leaflet.
function PopupDismissal() {
	const map = useMap();
	const { t } = useTranslation();

	useEffect(() => {
		const closeLabel = t("opportunities.mapPopupClose");

		function labelCloseButton() {
			map
				.getContainer()
				.querySelectorAll(".leaflet-popup-close-button")
				.forEach((button) => button.setAttribute("aria-label", closeLabel));
		}

		function handleKeyDown(event: KeyboardEvent) {
			if (event.key !== "Escape") return;
			const popup = map.getContainer().querySelector(".leaflet-popup");
			// Nothing open here - leave Escape to whatever overlay does own it.
			if (!popup) return;

			// Closing the popup destroys the element focus is sitting on, so
			// hand focus back to the marker that opened it rather than
			// dropping the keyboard user at the top of the document.
			const returnFocus =
				document.activeElement instanceof HTMLElement &&
				popup.contains(document.activeElement);
			map.closePopup();
			if (returnFocus) {
				map
					.getContainer()
					.querySelector<HTMLElement>(".leaflet-marker-icon")
					?.focus();
			}
		}

		map.on("popupopen", labelCloseButton);
		labelCloseButton();
		document.addEventListener("keydown", handleKeyDown);
		return () => {
			map.off("popupopen", labelCloseButton);
			document.removeEventListener("keydown", handleKeyDown);
		};
	}, [map, t]);

	return null;
}

interface Props {
	latitude: number;
	longitude: number;
	label: string;
}

export default function SingleMarkerMap({ latitude, longitude, label }: Props) {
	const brandMarker = useMemo(
		() =>
			L.divIcon({
				html: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 28 36" width="28" height="36" style="display:block" aria-hidden="true"><path d="M14 1C7.9 1 3 5.9 3 12c0 8.5 11 23 11 23S25 20.5 25 12C25 5.9 20.1 1 14 1z" fill="${brandColor("600")}" stroke="white" stroke-width="1.5"/><circle cx="14" cy="12" r="5" fill="white"/></svg>`,
				className: "",
				iconSize: [28, 36],
				iconAnchor: [14, 36],
				popupAnchor: [0, -38],
			}),
		[],
	);

	return (
		<div className="isolate h-64 w-full overflow-hidden rounded-card border border-gray-200 shadow-resting">
			<MapContainer
				center={[latitude, longitude]}
				zoom={14}
				dragging={false}
				scrollWheelZoom={false}
				doubleClickZoom={false}
				touchZoom={false}
				boxZoom={false}
				keyboard={false}
				zoomControl={false}
				className="h-full w-full"
			>
				<MapAccessibleName label={label} />
				<PopupDismissal />
				<TileLayer attribution={ATTRIBUTION} url={TILE_URL} />

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
