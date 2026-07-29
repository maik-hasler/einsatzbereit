import { useEffect, useRef, useState } from "react";
import { ChevronIcon, ChipXIcon, CheckMiniIcon } from "./icons";

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
	const [alignRight, setAlignRight] = useState(false);

	useEffect(() => {
		if (isOpen && containerRef.current) {
			const rect = containerRef.current.getBoundingClientRect();
			setAlignRight(rect.left + 300 > window.innerWidth - 8);
		}
	}, [isOpen]);

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
					className={`flex items-center gap-1.5 whitespace-nowrap py-1.5 text-sm transition-colors ${
						active
							? "pl-3 pr-1.5 font-medium text-brand-700"
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
					className={`absolute ${alignRight ? "right-0" : "left-0"} top-full z-[200] mt-1.5 overflow-hidden rounded-xl border border-gray-200 bg-white shadow-modal`}
				>
					{children}
				</div>
			)}
		</div>
	);
}
