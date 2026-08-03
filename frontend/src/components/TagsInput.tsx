import { useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import Chip from "./Chip";

interface Props {
	id: string;
	label: string;
	value: string[];
	onChange: (tags: string[]) => void;
	placeholder?: string;
	hint?: string;
}

export default function TagsInput({
	id,
	label,
	value,
	onChange,
	placeholder,
	hint,
}: Props) {
	const { t } = useTranslation();
	const [draft, setDraft] = useState("");
	const [statusMessage, setStatusMessage] = useState("");
	const inputRef = useRef<HTMLInputElement>(null);

	function commitDraft() {
		const trimmed = draft.trim();
		setDraft("");
		if (!trimmed) return;
		const alreadyPresent = value.some(
			(tag) => tag.toLowerCase() === trimmed.toLowerCase(),
		);
		if (alreadyPresent) {
			setStatusMessage(
				t("createOpportunity.tagAlreadyAdded", { tag: trimmed }),
			);
			return;
		}
		onChange([...value, trimmed]);
		setStatusMessage(t("createOpportunity.tagAdded", { tag: trimmed }));
	}

	function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
		if (e.key === "Enter" || e.key === ",") {
			e.preventDefault();
			commitDraft();
		} else if (e.key === "Backspace" && draft === "" && value.length > 0) {
			onChange(value.slice(0, -1));
		}
	}

	function removeTag(tag: string) {
		onChange(value.filter((existing) => existing !== tag));
		setStatusMessage(t("createOpportunity.tagRemoved", { tag }));
		// The clicked remove button unmounts along with its chip - without this,
		// focus would fall to <body> mid-form instead of somewhere deliberate.
		inputRef.current?.focus();
	}

	const hintId = hint ? `${id}-hint` : undefined;

	return (
		<div>
			<label
				htmlFor={id}
				className="mb-1.5 block text-sm font-semibold text-gray-800"
			>
				{label}
			</label>
			<div className="flex flex-wrap items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-3 py-2 shadow-sm transition focus-within:border-brand-400">
				{value.length > 0 && (
					// display:contents so this list wrapper (needed for a labelled
					// list rather than a bare run of <span> chips) doesn't
					// participate in the flex-wrap layout itself - its <li> children
					// do instead, same as before.
					<ul className="contents" aria-label={label}>
						{value.map((tag) => (
							<li key={tag} className="contents">
								<Chip
									tone="neutral"
									onRemove={() => removeTag(tag)}
									removeLabel={t("createOpportunity.removeTag", { tag })}
								>
									{tag}
								</Chip>
							</li>
						))}
					</ul>
				)}
				{/* The pill wrapper above shows the focus-within border; suppress
				the browser's own default focus box on this borderless, transparent
				input so it doesn't double up. The global :focus-visible ring
				(global.css, issue #992) still outlines the pill on keyboard focus. */}
				<input
					ref={inputRef}
					id={id}
					type="text"
					value={draft}
					onChange={(e) => setDraft(e.target.value)}
					onKeyDown={handleKeyDown}
					onBlur={commitDraft}
					aria-describedby={hintId}
					placeholder={value.length === 0 ? placeholder : undefined}
					className="min-w-32 flex-1 border-none bg-transparent py-0.5 text-sm text-gray-900 focus:ring-0 focus:outline-none"
				/>
			</div>
			{/* Always mounted (not conditional on `statusMessage`) so the live
			region is registered before it ever gets content - see
			CheckInModal.tsx's identical pattern for why. */}
			<p role="status" className="sr-only">
				{statusMessage}
			</p>
			{hint && (
				<p id={hintId} className="mt-1 text-xs text-gray-500">
					{hint}
				</p>
			)}
		</div>
	);
}
