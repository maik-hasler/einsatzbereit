import { useEffect, useState } from "react";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import {
	useCitySuggestions,
	type CitySuggestion,
} from "./VolunteerOpportunitiesList/useCitySuggestions";
import { MapPinIcon } from "./icons";

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
	const listboxId = `${id}-listbox`;

	const {
		suggestions,
		show: showSuggestions,
		setShow: setShowSuggestions,
		loading,
		error,
	} = useCitySuggestions(value);

	const statusMessage = loading
		? t("opportunities.citySearching")
		: error
			? error
			: value.length >= 2 && suggestions.length === 0
				? t("opportunities.cityNoMatch")
				: "";

	useEffect(() => {
		setActiveSuggestionIndex(-1);
	}, [suggestions]);

	function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
		if (!showSuggestions || suggestions.length === 0) return;
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
				setShowSuggestions(false);
				setActiveSuggestionIndex(-1);
				break;
		}
	}

	function select(suggestion: CitySuggestion) {
		onSelect(suggestion);
		setShowSuggestions(false);
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
				onChange={(e) => onValueChange(e.target.value)}
				onKeyDown={handleKeyDown}
				onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
				onFocus={() => {
					if (suggestions.length > 0) setShowSuggestions(true);
				}}
				className={inputClassName}
			/>
			{value && (
				<button
					type="button"
					onClick={() => onValueChange("")}
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
					className="absolute top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-gray-200 bg-white text-left shadow-modal"
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
