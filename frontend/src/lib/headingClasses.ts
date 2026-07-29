// Shared <h1> size/weight so every page picks the same scale instead of its
// own (see issue #981: text-xl/2xl/3xl/4xl all serving as "the page title").

/** Standard content-page heading (list/detail/settings pages). */
export const pageTitleClass = "text-2xl font-bold sm:text-3xl";

/** Centered single-message state heading (errors, 404, access-denied) - a
distinct role from a content page's title, kept visually larger. */
export const statusTitleClass = "text-3xl font-bold";
