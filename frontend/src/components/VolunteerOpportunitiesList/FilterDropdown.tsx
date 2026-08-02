import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { ChevronIcon, ChipXIcon, CheckMiniIcon } from "./icons";

const EDGE_MARGIN = 8;

export function DropdownOption({
	label,
	selected,
	onClick,
}: {
	label: string;
	selected: boolean;
	onClick: () => void;
}) {
	return (
		<button
			type="button"
			onClick={onClick}
			className={`flex w-full items-center gap-2 px-3.5 py-2 text-left text-sm transition-colors hover:bg-gray-50 ${
				selected ? "font-medium text-brand-700" : "text-gray-700"
			}`}
		>
			{selected && (
				<svg
					className="h-4 w-4 shrink-0 text-brand-600"
					viewBox="0 0 20 20"
					fill="currentColor"
					aria-hidden="true"
				>
					<path
						fillRule="evenodd"
						d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
						clipRule="evenodd"
					/>
				</svg>
			)}
			{label}
		</button>
	);
}

export function MultiDropdownOption({
	label,
	selected,
	onClick,
}: {
	label: string;
	selected: boolean;
	onClick: () => void;
}) {
	return (
		<button
			type="button"
			onClick={onClick}
			aria-pressed={selected}
			className={`flex w-full items-center gap-2.5 px-3.5 py-2 text-left text-sm transition-colors hover:bg-gray-50 ${
				selected ? "text-brand-700" : "text-gray-700"
			}`}
		>
			<span
				className={`flex h-4 w-4 shrink-0 items-center justify-center rounded border transition-colors ${
					selected
						? "border-brand-500 bg-brand-500"
						: "border-gray-300 bg-white"
				}`}
			>
				{selected && <CheckMiniIcon />}
			</span>
			{label}
		</button>
	);
}

export default function FilterDropdown({
	testId,
	icon,
	label,
	displayValue,
	isOpen,
	onToggle,
	onClear,
	clearAriaLabel,
	children,
}: {
	testId?: string;
	icon: React.ReactNode;
	label: string;
	displayValue: string;
	isOpen: boolean;
	onToggle: () => void;
	onClear: () => void;
	clearAriaLabel: string;
	children: React.ReactNode;
}) {
	const active = !!displayValue;
	const containerRef = useRef<HTMLDivElement>(null);
	const panelRef = useRef<HTMLDivElement>(null);
	const [panelLeft, setPanelLeft] = useState(0);

	useLayoutEffect(() => {
		const container = containerRef.current;
		const panel = panelRef.current;
		if (!isOpen || !container || !panel) return;

		const containerRect = container.getBoundingClientRect();
		const panelWidth = panel.getBoundingClientRect().width;

		// Prefer aligning the panel's left edge with the trigger's left edge,
		// flipping to the trigger's right edge only if that would overflow the
		// viewport - then clamp so neither edge can end up off-screen.
		const leftAligned = 0;
		const rightAligned = containerRect.width - panelWidth;
		const overflowsRight =
			containerRect.left + panelWidth > window.innerWidth - EDGE_MARGIN;
		const preferred = overflowsRight ? rightAligned : leftAligned;

		const minLeft = EDGE_MARGIN - containerRect.left;
		const maxLeft =
			window.innerWidth - EDGE_MARGIN - panelWidth - containerRect.left;
		setPanelLeft(Math.min(Math.max(preferred, minLeft), maxLeft));
	}, [isOpen]);

	// The panel is positioned relative to its trigger (top-full), not the
	// viewport, so scrolling drags it along instead of leaving it anchored in
	// place - close it rather than let it drift over the sticky header or the
	// results below (#1119). Skipped while focus is inside the panel: a
	// keyboard user tabbing through a panel taller than the viewport can
	// trigger the browser's own focus-follow auto-scroll, and closing under
	// them mid-navigation would drop focus to <body> with no warning.
	useEffect(() => {
		if (!isOpen) return;
		function handleScroll() {
			if (panelRef.current?.contains(document.activeElement)) return;
			onToggle();
		}
		window.addEventListener("scroll", handleScroll, { passive: true });
		return () => window.removeEventListener("scroll", handleScroll);
	}, [isOpen, onToggle]);

	return (
		<div ref={containerRef} className="relative shrink-0">
			<div
				role="group"
				aria-label={label}
				className={`inline-flex items-stretch overflow-hidden rounded-full border transition-all ${
					active
						? "border-brand-500 bg-brand-50"
						: "border-gray-200 bg-white hover:border-brand-300 hover:bg-brand-50/50"
				}`}
			>
				<button
					type="button"
					data-testid={testId}
					onClick={onToggle}
					aria-expanded={isOpen}
					className={`flex items-center gap-1.5 py-1.5 text-sm whitespace-nowrap transition-colors ${
						active
							? "pr-1.5 pl-3 font-medium text-brand-700"
							: "px-3 text-gray-600 hover:bg-gray-50"
					}`}
				>
					<span
						className={`shrink-0 ${active ? "text-brand-500" : "text-brand-400"}`}
						aria-hidden="true"
					>
						{icon}
					</span>
					<span>{active ? displayValue : label}</span>
					{!active && (
						<ChevronIcon
							className={`h-3 w-3 text-gray-400 transition-transform ${isOpen ? "rotate-180" : ""}`}
						/>
					)}
				</button>
				{active && (
					<button
						type="button"
						onClick={onClear}
						aria-label={clearAriaLabel}
						className="flex items-center px-2 py-1.5 text-brand-400 transition-colors hover:bg-brand-100 hover:text-brand-600"
					>
						<ChipXIcon />
					</button>
				)}
			</div>
			{isOpen && (
				<div
					ref={panelRef}
					style={{ left: panelLeft }}
					// Below Header.tsx's sticky z-40 - this panel's ancestors (the
					// filter bar, <main>) are all unpositioned, so its z-index
					// competes with the header directly at the document root instead
					// of nesting inside it (#1119).
					className="absolute top-full z-30 mt-1.5 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-modal"
				>
					{children}
				</div>
			)}
		</div>
	);
}
