// The one border/focus recipe every form control's invalid state must share
// (#2239) - a single source of truth so a red border on error never drifts
// between the wizard's floating fields and every other form's fields.
export function fieldBorderClass(hasError: boolean): string {
	return hasError
		? "border-red-300 focus:border-red-400"
		: "border-gray-200 focus:border-brand-400";
}

export function getInputSurfaceClass(hasError = false): string {
	return `min-h-10 w-full rounded-xl border ${fieldBorderClass(hasError)} bg-white px-3 py-2 text-sm shadow-sm transition`;
}

export function getInputClass(hasError = false): string {
	return `mt-1 block ${getInputSurfaceClass(hasError)} text-gray-900`;
}

export function getTextareaClass(hasError = false): string {
	return `${getInputClass(hasError)} resize-y`;
}

export function getLabelClass(hasError = false): string {
	return hasError
		? "block text-xs font-medium text-red-600"
		: "block text-xs font-medium text-gray-600";
}

export const inputSurfaceClass = getInputSurfaceClass();

export const inputClass = getInputClass();

export const textareaClass = getTextareaClass();

export const selectClass = `${inputClass} appearance-none bg-[length:1.25rem] bg-[right_0.5rem_center] bg-no-repeat pr-9 bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg%20xmlns=%22http://www.w3.org/2000/svg%22%20fill=%22none%22%20viewBox=%220%200%2024%2024%22%20stroke-width=%221.5%22%20stroke=%22%236b7280%22%3E%3Cpath%20stroke-linecap=%22round%22%20stroke-linejoin=%22round%22%20d=%22m19.5%208.25-7.5%207.5-7.5-7.5%22/%3E%3C/svg%3E')]`;

export const labelClass = getLabelClass();
