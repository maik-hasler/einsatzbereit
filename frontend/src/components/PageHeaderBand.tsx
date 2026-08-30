import type { ReactNode } from "react";
import { WAVE_PATH } from "../lib/wavePath";
import { useOverlaysHeader } from "../contexts/HeaderOverlayContext";
import { useQuickActionsList } from "../contexts/QuickActionsContext";
import Button from "./Button";

interface Props {
	eyebrow: ReactNode;
	title: string;

	// Rendered above the eyebrow, on the dark band - for a page whose subject
	// has a picture of its own (an organization's logo), not for decoration.
	avatar?: ReactNode;

	titleLang?: string;

	lead?: string;

	leadLang?: string;

	children?: ReactNode;

	fullWidth?: boolean;

	compactTitle?: boolean;
}

export default function PageHeaderBand({
	eyebrow,
	title,
	avatar,
	titleLang,
	lead,
	leadLang,
	children,
	fullWidth = false,
	compactTitle = false,
}: Props) {
	useOverlaysHeader();

	const actions = useQuickActionsList();

	return (
		<div className="relative left-1/2 -mt-[calc(var(--header-height)+var(--main-top-padding))] mb-12 w-screen -translate-x-1/2 sm:mb-16">
			<div className="relative isolate overflow-hidden bg-brand-800">
				<div
					aria-hidden="true"
					className="pointer-events-none absolute -top-32 -left-24 h-80 w-80 rounded-full bg-brand-700 opacity-60 blur-3xl"
				/>
				<div
					aria-hidden="true"
					className="pointer-events-none absolute -right-20 -bottom-32 h-72 w-72 rounded-full bg-accent-400 opacity-10 blur-3xl"
				/>

				<div className="relative mx-auto max-w-page px-4 sm:px-6 lg:px-8">
					<div
						className={`pt-[calc(var(--header-height)+1.5rem)] pb-10 sm:pt-[calc(var(--header-height)+2rem)] sm:pb-14 ${fullWidth ? "" : "mx-auto max-w-5xl"}`}
					>
						{actions.length > 0 && (
							<div className="animate-fade-up-d1 float-right ml-4 flex shrink-0 items-center gap-2">
								{actions.map((action) => (
									<Button
										key={action.key}
										type="button"
										onClick={action.onClick}
										disabled={action.disabled}
										title={action.title}
										aria-label={action.label}
										data-testid={`quick-action-${action.key}`}
										variant={
											action.variant === "primary" ? "onDark" : "outlineOnDark"
										}
										className="shrink-0"
									>
										{action.icon}
										<span className="hidden sm:inline">{action.label}</span>
									</Button>
								))}
							</div>
						)}
						{avatar && (
							<div className="animate-fade-up mb-4 w-fit rounded-full bg-white/10 p-1 ring-1 ring-white/30">
								{avatar}
							</div>
						)}
						<p className="animate-fade-up text-xs font-semibold tracking-widest text-brand-200 uppercase">
							{eyebrow}
						</p>
						<h1
							lang={titleLang}
							className={`animate-fade-up-d1 mt-3 max-w-4xl font-display font-bold tracking-tight text-white ${
								compactTitle
									? "text-3xl sm:text-4xl"
									: "text-5xl sm:text-6xl lg:text-7xl"
							}`}
						>
							{title}
						</h1>
						{lead && (
							<p
								lang={leadLang}
								className="animate-fade-up-d2 mt-5 max-w-2xl text-base leading-relaxed text-brand-100 sm:text-lg"
							>
								{lead}
							</p>
						)}
						{children && (
							<div className="animate-fade-up-d3 mt-6">{children}</div>
						)}
					</div>
				</div>
			</div>

			<svg
				aria-hidden="true"
				viewBox="0 0 1440 60"
				preserveAspectRatio="none"
				className="block h-8 w-full rotate-180 text-brand-800 sm:h-12"
			>
				<path d={WAVE_PATH} fill="currentColor" />
			</svg>
		</div>
	);
}
