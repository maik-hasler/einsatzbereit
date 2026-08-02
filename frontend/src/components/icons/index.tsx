import type { ReactNode } from "react";

// Single source for every icon glyph used across the app (#1115). Before
// this module existed, the same Heroicons path got hand-copied into up to
// six different files, each pasted copy free to drift to its own stroke
// weight or viewBox - a pin icon rendered at stroke 2 on one screen and
// stroke 1.5 one click later, a 10x10 checkmark rendered ~3.6x heavier than
// its 24x24 siblings, and so on. All stroke icons here share one 24x24
// viewBox and one stroke weight via StrokeIcon so that can't happen again;
// solid (filled) icons and the small drag-handle family are their own
// natural size since they're a different rendering style, not a stroke
// weight that can drift. Icons are decorative unless noted otherwise, so
// they default to aria-hidden.

function StrokeIcon({
	className = "h-5 w-5",
	children,
}: {
	className?: string;
	children: ReactNode;
}) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth={1.5}
			stroke="currentColor"
			aria-hidden="true"
		>
			{children}
		</svg>
	);
}

function SolidIcon({
	className = "h-5 w-5",
	viewBox = "0 0 24 24",
	children,
}: {
	className?: string;
	viewBox?: string;
	children: ReactNode;
}) {
	return (
		<svg
			className={className}
			viewBox={viewBox}
			fill="currentColor"
			aria-hidden="true"
		>
			{children}
		</svg>
	);
}

interface IconProps {
	className?: string;
}

interface ChevronIconProps extends IconProps {
	// Rotates the glyph 180deg (with a transition) when true - the same
	// open/closed indicator pattern repeated across every dropdown, filter,
	// and expandable menu in the header and filter bar.
	open?: boolean;
}

// ── Navigation / chrome ──────────────────────────────────────────────────

export function ChevronDownIcon({
	className = "h-3.5 w-3.5",
	open = false,
}: ChevronIconProps) {
	return (
		<StrokeIcon
			className={`${className} transition-transform ${open ? "rotate-180" : ""}`}
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m19.5 8.25-7.5 7.5-7.5-7.5"
			/>
		</StrokeIcon>
	);
}

export function ChevronLeftIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15.75 19.5 8.25 12l7.5-7.5"
			/>
		</StrokeIcon>
	);
}

export function ChevronRightIcon({
	className = "h-4 w-4",
	open = false,
}: ChevronIconProps) {
	return (
		<StrokeIcon
			className={`${className} transition-transform ${open ? "rotate-180" : ""}`}
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m8.25 4.5 7.5 7.5-7.5 7.5"
			/>
		</StrokeIcon>
	);
}

export function CloseIcon({ className = "h-5 w-5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6 18 18 6M6 6l12 12"
			/>
		</StrokeIcon>
	);
}

export function PlusIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M12 4.5v15m7.5-7.5h-15"
			/>
		</StrokeIcon>
	);
}

export function CheckIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M4.5 12.75l6 6 9-13.5"
			/>
		</StrokeIcon>
	);
}

export function Bars3Icon({ className = "h-6 w-6" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5"
			/>
		</StrokeIcon>
	);
}

export function HomeIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M2.25 12l8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25"
			/>
		</StrokeIcon>
	);
}

export function Squares2x2Icon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M3.75 6A2.25 2.25 0 0 1 6 3.75h2.25A2.25 2.25 0 0 1 10.5 6v2.25a2.25 2.25 0 0 1-2.25 2.25H6a2.25 2.25 0 0 1-2.25-2.25V6ZM3.75 15.75A2.25 2.25 0 0 1 6 13.5h2.25a2.25 2.25 0 0 1 2.25 2.25V18a2.25 2.25 0 0 1-2.25 2.25H6A2.25 2.25 0 0 1 3.75 18v-2.25ZM13.5 6a2.25 2.25 0 0 1 2.25-2.25H18A2.25 2.25 0 0 1 20.25 6v2.25A2.25 2.25 0 0 1 18 10.5h-2.25a2.25 2.25 0 0 1-2.25-2.25V6ZM13.5 15.75a2.25 2.25 0 0 1 2.25-2.25H18a2.25 2.25 0 0 1 2.25 2.25V18A2.25 2.25 0 0 1 18 20.25h-2.25A2.25 2.25 0 0 1 13.5 18v-2.25Z"
			/>
		</StrokeIcon>
	);
}

export function Cog6ToothIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.24-.438.613-.431.992a6.759 6.759 0 0 1 0 .255c-.007.378.138.75.43.99l1.005.828c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.57 6.57 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.28c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.992a6.932 6.932 0 0 1 0-.255c.007-.378-.138-.75-.43-.99l-1.004-.828a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.281Z"
			/>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
			/>
		</StrokeIcon>
	);
}

export function ArrowRightOnRectangleIcon({
	className = "h-4 w-4",
}: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25 2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15m3 0 3-3m0 0-3-3m3 3H9"
			/>
		</StrokeIcon>
	);
}

export function BellIcon({ className = "h-5 w-5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"
			/>
		</StrokeIcon>
	);
}

export function MagnifyingGlassIcon({ className = "h-6 w-6" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
			/>
		</StrokeIcon>
	);
}

export function ArrowRightIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3"
			/>
		</StrokeIcon>
	);
}

export function UserCircleIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z"
			/>
		</StrokeIcon>
	);
}

export function KeyIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15.75 5.25a3 3 0 0 1 3 3m3 0a6 6 0 0 1-7.029 5.912c-.563-.097-1.159.026-1.563.43L10.5 17.25H8.25v2.25H6v2.25H2.25v-2.818c0-.597.237-1.17.659-1.591l6.499-6.499c.404-.404.527-1 .43-1.563A6 6 0 1 1 21.75 8.25Z"
			/>
		</StrokeIcon>
	);
}

// ── Info glyphs ──────────────────────────────────────────────────────────

export function MapPinIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
			/>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
			/>
		</StrokeIcon>
	);
}

export function TagIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9.568 3H5.25A2.25 2.25 0 0 0 3 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 0 0 5.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 0 0 9.568 3Z"
			/>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6 6h.008v.008H6V6Z"
			/>
		</StrokeIcon>
	);
}

export function UsersIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z"
			/>
		</StrokeIcon>
	);
}

export function UserGroupIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z"
			/>
		</StrokeIcon>
	);
}

export function ClockIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
			/>
		</StrokeIcon>
	);
}

export function GlobeIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
			/>
		</StrokeIcon>
	);
}

export function CalendarIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5"
			/>
		</StrokeIcon>
	);
}

export function QrCodeIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M3.75 4.875c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5A1.125 1.125 0 0 1 3.75 9.375v-4.5ZM3.75 14.625c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5a1.125 1.125 0 0 1-1.125-1.125v-4.5ZM13.5 4.875c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5A1.125 1.125 0 0 1 13.5 9.375v-4.5Z"
			/>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6.75 6.75h.75v.75h-.75v-.75ZM6.75 16.5h.75v.75h-.75v-.75ZM16.5 6.75h.75v.75h-.75v-.75ZM13.5 13.5h.75v.75h-.75v-.75ZM13.5 19.5h.75v.75h-.75v-.75ZM19.5 13.5h.75v.75h-.75v-.75ZM19.5 19.5h.75v.75h-.75v-.75ZM16.5 16.5h.75v.75h-.75v-.75Z"
			/>
		</StrokeIcon>
	);
}

export function HashtagIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M5.25 8.25h15m-16.5 7.5h15m-1.8-13.5-3.9 19.5m-2.1-19.5-3.9 19.5"
			/>
		</StrokeIcon>
	);
}

export function FlagIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M3 3v18M3 4.5h13.5l-2.25 3.75 2.25 3.75H3"
			/>
		</StrokeIcon>
	);
}

export function EnvelopeIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75"
			/>
		</StrokeIcon>
	);
}

export function BuildingOfficeIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M2.25 21h19.5m-18-18v18m10.5-18v18m6-13.5V21M6.75 6.75h.75m-.75 3h.75m-.75 3h.75m3-6h.75m-.75 3h.75m-.75 3h.75M6.75 21v-3.375c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21M3 3h12m-.75 4.5H21m-3.75 3H21m-3.75 3H21"
			/>
		</StrokeIcon>
	);
}

export function PhoneIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M2.25 6.75c0 8.284 6.716 15 15 15h2.25a2.25 2.25 0 0 0 2.25-2.25v-1.372c0-.516-.351-.966-.852-1.091l-4.423-1.106c-.44-.11-.902.055-1.173.417l-.97 1.293c-.282.376-.769.542-1.21.38a12.035 12.035 0 0 1-7.143-7.143c-.162-.441.004-.928.38-1.21l1.293-.97c.363-.271.527-.734.417-1.173L6.963 3.102a1.125 1.125 0 0 0-1.091-.852H4.5A2.25 2.25 0 0 0 2.25 4.5v2.25Z"
			/>
		</StrokeIcon>
	);
}

export function PencilIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L6.832 19.82a4.5 4.5 0 0 1-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 0 1 1.13-1.897L16.863 4.487Zm0 0L19.5 7.125"
			/>
		</StrokeIcon>
	);
}

export function TrashIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6 7h12M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2M6 7l1 12.5A2 2 0 0 0 9 21h6a2 2 0 0 0 2-1.5L18 7M10 11v6M14 11v6"
			/>
		</StrokeIcon>
	);
}

export function ShareIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M7.217 10.907a2.25 2.25 0 1 0 0 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186 9.566-5.314m-9.566 7.5 9.566 5.314m0 0a2.25 2.25 0 1 0 3.935 2.186 2.25 2.25 0 0 0-3.935-2.186Zm0-12.814a2.25 2.25 0 1 0 3.933-2.185 2.25 2.25 0 0 0-3.933 2.185Z"
			/>
		</StrokeIcon>
	);
}

export function PhotoIcon({ className = "h-6 w-6" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909m-18 3.75h16.5a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5H3.75A1.5 1.5 0 0 0 2.25 6v12a1.5 1.5 0 0 0 1.5 1.5Zm10.5-11.25h.008v.008h-.008V8.25Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Z"
			/>
		</StrokeIcon>
	);
}

export function HandRaisedIcon({ className = "h-6 w-6" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M10.05 4.575a1.575 1.575 0 1 0-3.15 0v3m3.15-3v-1.5a1.575 1.575 0 0 1 3.15 0v1.5m-3.15 0 .075 5.925m3.075.75V4.575m0 0a1.575 1.575 0 0 1 3.15 0V15M6.9 7.575a1.575 1.575 0 1 0-3.15 0v8.175a6.75 6.75 0 0 0 6.75 6.75h2.018a5.25 5.25 0 0 0 3.712-1.538l1.732-1.732a5.25 5.25 0 0 0 1.538-3.712l.003-2.024a.668.668 0 0 1 .198-.471 1.575 1.575 0 1 0-2.228-2.228 3.818 3.818 0 0 0-1.12 2.687M6.9 7.575V12m6.27 4.318A4.49 4.49 0 0 1 16.35 15m.002 0h-.002"
			/>
		</StrokeIcon>
	);
}

export function SparklesIcon({ className = "h-6 w-6" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z"
			/>
		</StrokeIcon>
	);
}

export function BroomIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path d="m13 11 9-9" />
			<path d="M14.6 12.6c.8.8.9 2.1.2 3L10 22l-8-8 6.4-4.8c.9-.7 2.2-.6 3 .2Z" />
			<path d="m6.8 10.4 6.8 6.8" />
			<path d="m5 17 1.4-1.4" />
		</StrokeIcon>
	);
}

// ── Category glyphs (see VolunteerOpportunitiesList/CategoryGlyph.tsx) ────

export function AcademicCapIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5"
			/>
		</StrokeIcon>
	);
}

export function ShieldCheckIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9 12.75 11.25 15 15 9.75m-3-7.036A11.959 11.959 0 0 1 3.598 6 11.99 11.99 0 0 0 3 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285Z"
			/>
		</StrokeIcon>
	);
}

export function HeartIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z"
			/>
		</StrokeIcon>
	);
}

export function TrophyIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0"
			/>
		</StrokeIcon>
	);
}

export function MusicalNoteIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9 9l10.5-3m0 6.553v3.75a2.25 2.25 0 0 1-1.632 2.163l-1.32.377a1.803 1.803 0 1 1-.99-3.467l2.31-.66a2.25 2.25 0 0 0 1.632-2.163zm0 0V2.25L9 5.25v10.303m0 0v3.75a2.25 2.25 0 0 1-1.632 2.163l-1.32.377a1.803 1.803 0 0 1-.99-3.467l2.31-.66A2.25 2.25 0 0 0 9 15.553z"
			/>
		</StrokeIcon>
	);
}

export function ComputerDesktopIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<StrokeIcon className={className}>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25"
			/>
		</StrokeIcon>
	);
}

// ── Solid (filled) icons - own rendering style, not a stroke weight ───────

export function StarIcon({ className = "h-6 w-6" }: IconProps) {
	return (
		<SolidIcon className={className}>
			<path d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.006 5.404.434c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.434 2.082-5.005Z" />
		</SolidIcon>
	);
}

// Heroicons' 20x20 "mini" set, not the 24x24 outline family above - kept at
// its own native size rather than force-fit into the 24 grid, same as
// Heroicons itself ships it (a solid fill has no stroke weight to drift).
export function CheckIconSolid({ className = "h-4 w-4" }: IconProps) {
	return (
		<SolidIcon className={className} viewBox="0 0 20 20">
			<path
				fillRule="evenodd"
				d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
				clipRule="evenodd"
			/>
		</SolidIcon>
	);
}

export function PawIcon({ className = "h-10 w-10" }: IconProps) {
	return (
		<SolidIcon className={className}>
			<ellipse cx="12" cy="16" rx="4" ry="3.2" />
			<circle cx="6.5" cy="11" r="1.9" />
			<circle cx="17.5" cy="11" r="1.9" />
			<circle cx="9" cy="7" r="1.9" />
			<circle cx="15" cy="7" r="1.9" />
		</SolidIcon>
	);
}

// ── Drag-handle family (org dashboard widgets) - own 16x16 glyphs, not
// part of the 24x24 outline set above ──────────────────────────────────

export function GripDotsIcon({ className = "h-4 w-4" }: IconProps) {
	return (
		<svg
			className={className}
			viewBox="0 0 16 16"
			fill="currentColor"
			aria-hidden="true"
		>
			<circle cx="5" cy="3" r="1.25" />
			<circle cx="11" cy="3" r="1.25" />
			<circle cx="5" cy="8" r="1.25" />
			<circle cx="11" cy="8" r="1.25" />
			<circle cx="5" cy="13" r="1.25" />
			<circle cx="11" cy="13" r="1.25" />
		</svg>
	);
}

export function ResizeHandleIcon({ className = "h-3.5 w-3.5" }: IconProps) {
	return (
		<svg
			className={className}
			viewBox="0 0 16 16"
			fill="none"
			stroke="currentColor"
			strokeWidth="1.5"
			strokeLinecap="round"
			aria-hidden="true"
		>
			<path d="M13 3 3 13M13 8 8 13" />
		</svg>
	);
}
