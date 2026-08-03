// Focus-visible styling comes from the global :focus-visible ring in
// global.css (issue #992), not from a low-contrast focus:ring-*/outline-none
// pair here - see Button.tsx's BASE_CLASSES for the same reasoning.

// Shared surface recipe (border/radius/background/shadow/focus) for text
// inputs and select-like triggers alike - kept separate from inputClass's
// own `block`/`mt-1` layout classes so a flex-based trigger (e.g. Dropdown)
// can reuse the same look without a block/flex classname contradiction
// (einsatzbereit#1104).
export const inputSurfaceClass =
	"w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400";

export const inputClass = `mt-1 block ${inputSurfaceClass} text-gray-900`;

export const textareaClass = `${inputClass} resize-y`;

export const labelClass = "block text-xs font-medium text-gray-600";
