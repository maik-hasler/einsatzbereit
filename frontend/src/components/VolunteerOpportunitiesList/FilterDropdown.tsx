import { useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { resolveDropdownPlacement } from "../../lib/dropdownPlacement";
import {
	CheckIcon,
	CheckIconSolid,
	ChevronDownIcon,
	CloseIcon,
} from "../icons";

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
			aria-pressed={selected}
			className={`flex w-full items-center gap-2 px-3.5 py-2 text-left text-sm transition-colors hover:bg-gray-50 ${
				selected ? "font-medium text-brand-700" : "text-gray-700"
			}`}
		>
			{selected && (
				<CheckIconSolid className="h-4 w-4 shrink-0 text-brand-600" />
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
						? "border-brand-600 bg-brand-600"
						: "border-gray-500 bg-white"
				}`}
			>
				{selected && <CheckIcon className="h-2.5 w-2.5 text-white" />}
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
	onClose,
	onClear,
	clearAriaLabel,
	allowOverflow = false,
	children,
}: {
	testId?: string;
	icon: React.ReactNode;
	label: string;
	displayValue: string;
	isOpen: boolean;
	onToggle: () => void;
	/**
	 * Closes this dropdown when focus leaves it. The list's shared outside-click
	 * handler covers the filter bar as a whole, so it never fires while focus is
	 * still on a sibling chip - which is exactly the case that left a panel
	 * floating over the chip the user had just tabbed to (#2327).
	 */
	onClose: () => void;
	onClear: () => void;
	clearAriaLabel: string;
	/**
	 * Lets a panel child escape the panel's bounds. The panel clips by default so that
	 * an option list running edge to edge stays inside the rounded corners, but a child
	 * that deliberately overflows - the city autocomplete's listbox - was sliced through
	 * the middle of its last row instead, with nothing to hint the list continued (#2319).
	 */
	allowOverflow?: boolean;
	children: React.ReactNode;
}) {
	const active = !!displayValue;
	const containerRef = useRef<HTMLDivElement>(null);
	const panelRef = useRef<HTMLDivElement>(null);
	const [panelLeft, setPanelLeft] = useState(0);
	const [dropUp, setDropUp] = useState(false);
	const panelId = useId();
	const onCloseRef = useRef(onClose);
	onCloseRef.current = onClose;

	useEffect(() => {
		if (!isOpen) return;

		function handleFocusIn(e: FocusEvent) {
			const target = e.target;
			if (!(target instanceof Node)) return;
			if (containerRef.current?.contains(target)) return;
			onCloseRef.current();
		}

		document.addEventListener("focusin", handleFocusIn);
		return () => document.removeEventListener("focusin", handleFocusIn);
	}, [isOpen]);

	useLayoutEffect(() => {
		const container = containerRef.current;
		const panel = panelRef.current;
		if (!isOpen || !container || !panel) return;

		const containerRect = container.getBoundingClientRect();
		const panelRect = panel.getBoundingClientRect();
		const panelWidth = panelRect.width;

		const leftAligned = 0;
		const rightAligned = containerRect.width - panelWidth;
		const overflowsRight =
			containerRect.left + panelWidth > window.innerWidth - EDGE_MARGIN;
		const preferred = overflowsRight ? rightAligned : leftAligned;

		const minLeft = EDGE_MARGIN - containerRect.left;
		const maxLeft =
			window.innerWidth - EDGE_MARGIN - panelWidth - containerRect.left;
		setPanelLeft(Math.min(Math.max(preferred, minLeft), maxLeft));

		// The panel was only ever nudged sideways, so on a phone a tall one (the date
		// picker is ~302px) opened past the bottom of the viewport with its last week
		// row, legend and selected-range footer off-screen and nothing scrolling them
		// into view (#2319). Flip it above the chip when that side has the room.
		setDropUp(
			resolveDropdownPlacement({
				triggerTop: containerRect.top,
				triggerBottom: containerRect.bottom,
				panelHeight: panelRect.height,
				viewportHeight: window.innerHeight,
				edgeMargin: EDGE_MARGIN,
			}) === "above",
		);
	}, [isOpen]);

	return (
		<div ref={containerRef} className="relative shrink-0">
			<div
				role="group"
				aria-label={label}
				className={`inline-flex items-stretch overflow-hidden rounded-full border bg-white transition-all ${
					active
						? "border-brand-600 shadow-resting"
						: "border-gray-500 hover:border-brand-600 hover:shadow-resting"
				}`}
			>
				<button
					type="button"
					data-testid={testId}
					onClick={onToggle}
					aria-expanded={isOpen}
					aria-controls={panelId}
					className={`flex items-center gap-1.5 py-1.5 text-sm whitespace-nowrap transition-colors ${
						active
							? "pr-1.5 pl-3 font-medium text-brand-700"
							: "px-3 text-gray-600 hover:bg-gray-50"
					}`}
				>
					<span
						className={`shrink-0 ${active ? "text-brand-700" : "text-brand-600"}`}
						aria-hidden="true"
					>
						{icon}
					</span>
					<span>{active ? displayValue : label}</span>
					{!active && (
						<ChevronDownIcon open={isOpen} className="h-3 w-3 text-gray-500" />
					)}
				</button>
				{active && (
					<button
						type="button"
						onClick={onClear}
						aria-label={clearAriaLabel}
						className="flex items-center px-2 py-1.5 text-brand-700 transition-colors hover:bg-brand-100 hover:text-brand-800"
					>
						<CloseIcon className="h-3 w-3" />
					</button>
				)}
			</div>
			{isOpen && (
				<div
					id={panelId}
					ref={panelRef}
					role="group"
					aria-label={label}
					style={{ left: panelLeft }}

					className={`absolute z-30 rounded-xl border border-gray-500 bg-white shadow-modal ${
						dropUp ? "bottom-full mb-1.5" : "top-full mt-1.5"
					} ${allowOverflow ? "" : "overflow-hidden"}`}
				>
					{children}
				</div>
			)}
		</div>
	);
}
