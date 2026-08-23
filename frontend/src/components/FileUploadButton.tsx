import type { ChangeEventHandler, RefObject } from "react";

const LABEL_CLASSES =
	"cursor-pointer rounded-xl border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50";

interface Props {
	id: string;
	label: string;
	accept: string;
	onChange: ChangeEventHandler<HTMLInputElement>;
	disabled?: boolean;
	inputRef?: RefObject<HTMLInputElement | null>;

	ariaDescribedBy?: string;
}

export default function FileUploadButton({
	id,
	label,
	accept,
	onChange,
	disabled = false,
	inputRef,
	ariaDescribedBy,
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
