import { useState } from "react";
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

	function commitDraft() {
		const trimmed = draft.trim();
		setDraft("");
		if (!trimmed) return;
		const alreadyPresent = value.some(
			(tag) => tag.toLowerCase() === trimmed.toLowerCase(),
		);
		if (alreadyPresent) return;
		onChange([...value, trimmed]);
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
			<div className="flex flex-wrap items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-3 py-2 shadow-sm transition focus-within:border-brand-400 focus-within:ring-2 focus-within:ring-brand-400/30">
				{value.map((tag) => (
					<Chip
						key={tag}
						tone="neutral"
						onRemove={() => removeTag(tag)}
						removeLabel={t("createOpportunity.removeTag", { tag })}
					>
						{tag}
					</Chip>
				))}
				<input
					id={id}
					type="text"
					value={draft}
					onChange={(e) => setDraft(e.target.value)}
					onKeyDown={handleKeyDown}
					onBlur={commitDraft}
					aria-describedby={hintId}
					placeholder={value.length === 0 ? placeholder : undefined}
					className="min-w-32 flex-1 border-none bg-transparent py-0.5 text-sm text-gray-900 focus:outline-none focus:ring-0"
				/>
			</div>
			{hint && (
				<p id={hintId} className="mt-1 text-xs text-gray-500">
					{hint}
				</p>
			)}
		</div>
	);
}
