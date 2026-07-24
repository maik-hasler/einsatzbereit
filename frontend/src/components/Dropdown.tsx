import { useEffect, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import { useDismissableOverlay } from "../hooks/useDismissableOverlay";

export interface DropdownOption {
	value: string;
	label: string;
	disabled?: boolean;
}

interface DropdownProps {
	id: string;
	value: string;
	onChange: (value: string) => void;
	options: DropdownOption[];
	placeholder?: string;
	disabled?: boolean;
	className?: string;
}

function firstEnabledIndex(options: DropdownOption[]): number {
	return options.findIndex((o) => !o.disabled);
}

function lastEnabledIndex(options: DropdownOption[]): number {
	for (let i = options.length - 1; i >= 0; i--) {
		if (!options[i].disabled) return i;
	}
	return -1;
}

/**
 * Accessible replacement for a native <select>, styled to match the app's
 * inputs instead of falling back to the browser/OS picker. Implements the
 * WAI-ARIA "select-only combobox" pattern - focus stays on the trigger
 * button and the active option is tracked via aria-activedescendant.
 */
export default function Dropdown({
	id,
	value,
	onChange,
	options,
	placeholder,
	disabled = false,
	className = "",
}: DropdownProps) {
	const [open, setOpen] = useState(false);
	const [activeIndex, setActiveIndex] = useState(-1);
	const rootRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);
	const listRef = useRef<HTMLUListElement>(null);
	const typeaheadBuffer = useRef("");
	const typeaheadTimeout = useRef<ReturnType<typeof setTimeout> | undefined>(
		undefined,
	);
	const listboxId = `${id}-listbox`;

	const selectedIndex = options.findIndex((o) => o.value === value);
	const selected = selectedIndex >= 0 ? options[selectedIndex] : undefined;

	useEffect(() => {
		if (open) {
			setActiveIndex(
				selectedIndex >= 0 ? selectedIndex : firstEnabledIndex(options),
			);
		}
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [open]);

	useEffect(() => {
		if (!open || activeIndex < 0) return;
		const el = listRef.current?.children[activeIndex] as
			HTMLElement | undefined;
		el?.scrollIntoView({ block: "nearest" });
	}, [open, activeIndex]);

	function moveActive(delta: number) {
		if (options.every((o) => o.disabled)) return;
		setActiveIndex((prev) => {
			let next = prev;
			for (let i = 0; i < options.length; i++) {
				next = (next + delta + options.length) % options.length;
				if (!options[next].disabled) return next;
			}
			return prev;
		});
	}

	function commit(index: number) {
		const opt = options[index];
		if (!opt || opt.disabled) return;
		onChange(opt.value);
		setOpen(false);
	}

	function handleTypeahead(char: string) {
		window.clearTimeout(typeaheadTimeout.current);
		typeaheadBuffer.current += char.toLowerCase();
		const buffer = typeaheadBuffer.current;
		typeaheadTimeout.current = setTimeout(() => {
			typeaheadBuffer.current = "";
		}, 500);
		const startFrom = (open ? activeIndex : selectedIndex) + 1;
		for (let i = 0; i < options.length; i++) {
			const idx = (startFrom + i) % options.length;
			const opt = options[idx];
			if (!opt.disabled && opt.label.toLowerCase().startsWith(buffer)) {
				if (open) setActiveIndex(idx);
				else onChange(opt.value);
				return;
			}
		}
	}

	function handleKeyDown(e: ReactKeyboardEvent<HTMLButtonElement>) {
		switch (e.key) {
			case "ArrowDown":
				e.preventDefault();
				if (!open) setOpen(true);
				else moveActive(1);
				break;
			case "ArrowUp":
				e.preventDefault();
				if (!open) setOpen(true);
				else moveActive(-1);
				break;
			case "Home":
				if (open) {
					e.preventDefault();
					setActiveIndex(firstEnabledIndex(options));
				}
				break;
			case "End":
				if (open) {
					e.preventDefault();
					setActiveIndex(lastEnabledIndex(options));
				}
				break;
			case "Enter":
			case " ":
				e.preventDefault();
				if (open) commit(activeIndex);
				else setOpen(true);
				break;
			case "Tab":
				setOpen(false);
				break;
			default:
				if (e.key.length === 1 && !e.altKey && !e.ctrlKey && !e.metaKey) {
					handleTypeahead(e.key);
				}
		}
	}

	return (
		<div className="relative" ref={rootRef}>
			<button
				type="button"
				id={id}
				role="combobox"
				aria-haspopup="listbox"
				aria-expanded={open}
				aria-controls={listboxId}
				aria-activedescendant={
					open && activeIndex >= 0 ? `${id}-option-${activeIndex}` : undefined
				}
				disabled={disabled}
				onClick={() => setOpen((o) => !o)}
				onKeyDown={handleKeyDown}
				className={`flex w-full items-center justify-between gap-2 text-left disabled:cursor-not-allowed disabled:opacity-50 ${className}`}
			>
				<span className={`truncate ${selected ? "" : "text-gray-400"}`}>
					{selected ? selected.label : (placeholder ?? "")}
				</span>
				<svg
					aria-hidden="true"
					className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${open ? "rotate-180" : ""}`}
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="2"
					stroke="currentColor"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="m19.5 8.25-7.5 7.5-7.5-7.5"
					/>
				</svg>
			</button>

			{open && (
				<ul
					ref={listRef}
					id={listboxId}
					role="listbox"
					aria-labelledby={id}
					className="absolute left-0 top-full z-50 mt-1 max-h-56 w-full overflow-auto rounded-lg border border-gray-200 bg-white py-1 text-sm shadow-lg"
				>
					{options.map((opt, index) => (
						<li
							key={opt.value}
							id={`${id}-option-${index}`}
							role="option"
							aria-selected={opt.value === value}
							aria-disabled={opt.disabled || undefined}
							onMouseEnter={() => !opt.disabled && setActiveIndex(index)}
							onMouseDown={(e) => {
								// Keep focus on the trigger button (combobox pattern)
								// instead of letting the option steal it.
								e.preventDefault();
								commit(index);
							}}
							className={`px-3 py-2 ${
								opt.disabled
									? "cursor-not-allowed text-gray-300"
									: `cursor-pointer ${
											index === activeIndex
												? "bg-brand-50 text-brand-700"
												: "text-gray-700 hover:bg-gray-50"
										}`
							}`}
						>
							{opt.label}
						</li>
					))}
				</ul>
			)}
		</div>
	);
}
