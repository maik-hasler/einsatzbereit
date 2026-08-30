import { useEffect, useState } from "react";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import {
	useCitySuggestions,
	type CitySuggestion,
} from "./VolunteerOpportunitiesList/useCitySuggestions";
import { MapPinIcon } from "./icons";

// A short query that matches nothing yet may still complete into a real city
// as the user keeps typing - only assert "no match" once the query is long
// enough that it's unlikely to still be a mid-word prefix (#2227).
const MIN_CONFIDENT_NO_MATCH_LENGTH = 3;

export default function LocationSearchInput({
	id,
	value,
	onValueChange,
	onSelect,
	placeholder,
	ariaLabel,
	inputClassName,
}: {
	id: string;
	value: string;
	onValueChange: (value: string) => void;
	onSelect: (suggestion: CitySuggestion) => void;
	placeholder: string;
	ariaLabel: string;
	inputClassName: string;
}) {
	const { t } = useTranslation();
	const [activeSuggestionIndex, setActiveSuggestionIndex] = useState(-1);
	// Opening the list is a response to the user asking for it - typing, or focusing a
	// field that already has matches - never to suggestions merely arriving. Letting the
	// lookup itself open the list meant remounting this input with an already-committed
	// city (reopening the filter panel, or loading a shared ?city= URL) popped a one-item
	// dropdown over the "near me" button underneath it and swallowed clicks on it (#2319).
	const [isListOpen, setIsListOpen] = useState(false);
	const listboxId = `${id}-listbox`;

	const { suggestions, searched, loading, error } = useCitySuggestions(value);

	const showSuggestions = isListOpen && suggestions.length > 0;

	const statusMessage = loading
		? t("opportunities.citySearching")
		: error
			? error
			: suggestions.length > 0
				? ""
				: searched && value.length >= MIN_CONFIDENT_NO_MATCH_LENGTH
					? t("opportunities.cityNoMatch")
					: value.length >= 2
						? t("opportunities.cityKeepTyping")
						: "";

	useEffect(() => {
		setActiveSuggestionIndex(-1);
	}, [suggestions]);

	function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
		if (!showSuggestions) return;
		switch (e.key) {
			case "ArrowDown":
				e.preventDefault();
				setActiveSuggestionIndex((i) => (i + 1) % suggestions.length);
				break;
			case "ArrowUp":
				e.preventDefault();
				setActiveSuggestionIndex(
					(i) => (i - 1 + suggestions.length) % suggestions.length,
				);
				break;
			case "Enter":
				if (activeSuggestionIndex >= 0) {
					e.preventDefault();
					select(suggestions[activeSuggestionIndex]);
				}
				break;
			case "Escape":
				e.preventDefault();
				e.stopPropagation();
				setIsListOpen(false);
				setActiveSuggestionIndex(-1);
				break;
		}
	}

	function select(suggestion: CitySuggestion) {
		onSelect(suggestion);
		setIsListOpen(false);
	}

	return (
		<div className="relative">
			<MapPinIcon className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-gray-400" />
			<input
				id={id}
				type="text"
				role="combobox"
				aria-label={ariaLabel}
				aria-expanded={showSuggestions}
				aria-controls={listboxId}
				aria-autocomplete="list"
				aria-activedescendant={
					showSuggestions && activeSuggestionIndex >= 0
						? `${listboxId}-option-${activeSuggestionIndex}`
						: undefined
				}
				placeholder={placeholder}
				value={value}
				onChange={(e) => {
					setIsListOpen(true);
					onValueChange(e.target.value);
				}}
				onKeyDown={handleKeyDown}
				onBlur={() => setTimeout(() => setIsListOpen(false), 150)}
				onFocus={() => setIsListOpen(true)}
				className={inputClassName}
			/>
			{value && (
				<button
					type="button"
					onClick={() => {
						setIsListOpen(false);
						onValueChange("");
					}}
					aria-label={t("opportunities.clearCity")}
					className="absolute top-1/2 right-2.5 -translate-y-1/2 text-gray-600 hover:text-gray-800"
				>
					&times;
				</button>
			)}
			{showSuggestions && (
				<ul
					id={listboxId}
					role="listbox"
					aria-label={ariaLabel}
					className="absolute top-full z-30 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-gray-200 bg-white text-left shadow-modal"
				>
					{suggestions.map((s, i) => {
						const isExactTypedMatch =
							s.label.trim().toLowerCase() === value.trim().toLowerCase();
						return (
							<li
								key={i}
								id={`${listboxId}-option-${i}`}
								role="option"
								aria-selected={i === activeSuggestionIndex}
								onMouseDown={(e) => e.preventDefault()}
								onMouseEnter={() => setActiveSuggestionIndex(i)}
								onClick={() => select(s)}

								onKeyDown={(e) => {
									if (e.key === "Enter") select(s);
								}}
								className={`cursor-pointer px-3 py-2 text-sm text-gray-700 ${
									i === activeSuggestionIndex
										? "bg-brand-50 text-brand-700"
										: "hover:bg-brand-50 hover:text-brand-700"
								}`}
							>
								<span className="flex items-center gap-2">
									<MapPinIcon className="h-3.5 w-3.5 shrink-0 text-gray-400" />
									<span className="flex flex-col">
										<span>{s.label}</span>
										{isExactTypedMatch && (
											<span className="text-xs text-gray-500">
												{t("opportunities.cityExactNameMatch")}
											</span>
										)}
									</span>
								</span>
							</li>
						);
					})}
				</ul>
			)}
			<p
				role="status"
				className={statusMessage ? "mt-1.5 text-xs text-gray-500" : "sr-only"}
			>
				{statusMessage}
			</p>
		</div>
	);
}
