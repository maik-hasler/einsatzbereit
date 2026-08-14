import L from "leaflet";
import { brandColor } from "./brandColor";

/**
 * The brand-colored teardrop pin shared by every Leaflet map in the app
 * (SingleMarkerMap.tsx's detail-page map, OpportunityResultsMap.tsx's
 * multi-pin browse map) - extracted so the marker SVG has one definition
 * instead of drifting into hand-copied variants (see frontend/AGENTS.md's
 * Design System section).
 *
 * Call from `useMemo` at the call site (not module scope): brandColor()'s
 * getComputedStyle read needs the component to have actually mounted in the
 * browser first, once global.css's @theme tokens are guaranteed to be
 * applied (#1129).
 */
export function createBrandMarkerIcon(): L.DivIcon {
	return L.divIcon({
		html: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 28 36" width="28" height="36" style="display:block" aria-hidden="true"><path d="M14 1C7.9 1 3 5.9 3 12c0 8.5 11 23 11 23S25 20.5 25 12C25 5.9 20.1 1 14 1z" fill="${brandColor("600")}" stroke="white" stroke-width="1.5"/><circle cx="14" cy="12" r="5" fill="white"/></svg>`,
		className: "",
		iconSize: [28, 36],
		iconAnchor: [14, 36],
		popupAnchor: [0, -38],
	});
}
