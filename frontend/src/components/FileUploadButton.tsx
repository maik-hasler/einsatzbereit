import type { ChangeEventHandler, RefObject } from "react";

// Shared button-styled trigger for a hidden (sr-only) file input - see issue
// #1131: three different visual treatments (border radius, color, missing
// transition) for the identical avatar/logo upload control before this
// existed, following the same pattern as Button.tsx's BASE_CLASSES (#846/#847).
const LABEL_CLASSES =
	"cursor-pointer rounded-xl border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50";

interface Props {
	id: string;
	label: string;
	accept: string;
	onChange: ChangeEventHandler<HTMLInputElement>;
	disabled?: boolean;
	inputRef?: RefObject<HTMLInputElement | null>;
	/** Id of a `role="alert"` element describing the current validation error, if any. */
	"aria-describedby"?: string;
}

export default function FileUploadButton({
	id,
	label,
	accept,
	onChange,
	disabled = false,
	inputRef,
	"aria-describedby": ariaDescribedBy,
}: Props) {
	return (
		<>
			<label
				htmlFor={id}
				className={`${LABEL_CLASSES} ${disabled ? "pointer-events-none opacity-50" : ""}`}
			>
				{label}
			</label>
			<input
				ref={inputRef}
				id={id}
				type="file"
				accept={accept}
				className="sr-only"
				onChange={onChange}
				disabled={disabled}
				aria-invalid={ariaDescribedBy ? true : undefined}
				aria-describedby={ariaDescribedBy}
			/>
		</>
	);
}
