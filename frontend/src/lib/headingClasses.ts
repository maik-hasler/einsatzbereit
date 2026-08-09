// Shared <h1> size/weight so every page picks the same scale instead of its
// own (see issue #981: text-xl/2xl/3xl/4xl all serving as "the page title").
//
// Both roles moved onto --font-display (Barlow Condensed) in #1755. The
// landing page had been the only surface using the display face at all, so
// every other page announced itself in the same Source Sans 3 at text-2xl as
// its own body copy - the single biggest reason the subpages read as a
// different, flatter product than the page visitors arrive from. Barlow
// Condensed is narrow enough that these larger steps occupy roughly the width
// the old text-2xl/3xl did in the body face, so the bump buys presence without
// forcing titles onto a second line.

/** Standard content-page heading (list/detail/settings pages). */
export const pageTitleClass =
	"font-display text-4xl font-bold tracking-tight sm:text-5xl";

/** Centered single-message state heading (errors, 404, access-denied) - a
distinct role from a content page's title, kept visually larger. */
export const statusTitleClass =
	"font-display text-5xl font-bold tracking-tight sm:text-6xl";
