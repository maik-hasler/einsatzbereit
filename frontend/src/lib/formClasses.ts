// Focus-visible styling comes from the global :focus-visible ring in
// global.css (issue #992), not from a low-contrast focus:ring-*/outline-none
// pair here - see Button.tsx's BASE_CLASSES for the same reasoning.
export const inputClass =
	"mt-1 block w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm transition focus:border-brand-400";

export const textareaClass = `${inputClass} resize-y`;

export const labelClass = "block text-xs font-medium text-gray-600";
