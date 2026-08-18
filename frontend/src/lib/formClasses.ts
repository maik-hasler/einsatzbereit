// Focus-visible styling comes from the global :focus-visible ring in
// global.css (issue #992), not from a low-contrast focus:ring-*/outline-none
// pair here - see Button.tsx's BASE_CLASSES for the same reasoning.

// Shared surface recipe (border/radius/background/shadow/focus) for text
// inputs and select-like triggers alike - kept separate from inputClass's
// own `block`/`mt-1` layout classes so a flex-based trigger (e.g. Dropdown)
// can reuse the same look without a block/flex classname contradiction
// (einsatzbereit#1104).
// min-h-10 (not a fixed h-10) gives text inputs and native <select>s the
// same floor height regardless of each browser's own select chrome (which
// otherwise renders a couple pixels taller than a same-padded input) - see
// Button.tsx's md size for the matching half of this fix (issue #1673:
// three different control heights staggered in one filter row).
export const inputSurfaceClass =
	"min-h-10 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400";

export const inputClass = `mt-1 block ${inputSurfaceClass} text-gray-900`;

export const textareaClass = `${inputClass} resize-y`;

// Native <select>s were left entirely unstyled in four places (the org app's
// Sign-ups status filter and Members invite-role picker, plus the engagement
// management page's two), so they rendered in raw OS chrome next to fully
// styled inputs. appearance-none plus an inline chevron gives them the same
// surface as everything else; bg-position keeps the chevron clear of the
// text on both paddings.
export const selectClass = `${inputClass} appearance-none bg-[length:1.25rem] bg-[right_0.5rem_center] bg-no-repeat pr-9 bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg%20xmlns=%22http://www.w3.org/2000/svg%22%20fill=%22none%22%20viewBox=%220%200%2024%2024%22%20stroke-width=%221.5%22%20stroke=%22%236b7280%22%3E%3Cpath%20stroke-linecap=%22round%22%20stroke-linejoin=%22round%22%20d=%22m19.5%208.25-7.5%207.5-7.5-7.5%22/%3E%3C/svg%3E')]`;

export const labelClass = "block text-xs font-medium text-gray-600";
