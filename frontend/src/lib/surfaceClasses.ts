// Shared card/panel recipe so every bordered content surface picks the same
// radius, border, background and shadow instead of its own slightly
// different hand-rolled combination (see issue #1106: eight incompatible
// card recipes, five of them on a single page). Card-like surfaces whose
// padding lives on an inner content wrapper instead of the outer frame
// (media cards with an edge-to-edge banner image or map) aren't covered
// here - the drift that mattered was the text/panel role, not those.

/** Standard elevated card surface - list items, result cards, widget panels. */
export const cardClass =
	"rounded-card border border-gray-100 bg-white p-4 shadow-resting";

/** Flat "info panel" variant - no shadow, tinted background, same radius/border. */
export const cardSubtleClass =
	"rounded-card border border-gray-100 bg-gray-50 p-4";
