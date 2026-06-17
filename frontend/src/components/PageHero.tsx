import { Link } from "react-router";

interface Props {
	title: string;
	subtitle?: string;
	icon?: React.ReactNode;
	actions?: React.ReactNode;
	children?: React.ReactNode;
	backHref?: string;
	backLabel?: string;
}

export default function PageHero({
	title,
	subtitle,
	icon,
	actions,
	children,
	backHref,
	backLabel,
}: Props) {
	return (
		<section className="-mx-4 -mt-6 relative mb-8 overflow-hidden bg-brand-800 px-4 pb-16 pt-8 sm:-mx-6 sm:-mt-10 sm:px-6 sm:pt-10 lg:-mx-8 lg:-mt-12 lg:px-8 lg:pt-12">
			<div
				className="pointer-events-none absolute -right-24 -top-16 h-72 w-72 rounded-full bg-brand-700 opacity-30 blur-3xl"
				aria-hidden="true"
			/>
			<div
				className="pointer-events-none absolute -left-12 bottom-4 h-56 w-56 rounded-full bg-brand-600 opacity-20 blur-3xl"
				aria-hidden="true"
			/>
			<div
				className="pointer-events-none absolute left-1/2 top-0 h-40 w-48 -translate-x-1/2 rounded-full bg-accent-400 opacity-10 blur-3xl"
				aria-hidden="true"
			/>

			<div className="relative mx-auto max-w-7xl">
				{backHref && backLabel && (
					<Link
						to={backHref}
						className="mb-5 inline-flex items-center gap-1.5 text-sm text-brand-200 transition-colors hover:text-white"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="2"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M10.5 19.5 3 12m0 0 7.5-7.5M3 12h18"
							/>
						</svg>
						{backLabel}
					</Link>
				)}

				<div className="flex flex-wrap items-center justify-between gap-4">
					<div className="flex items-center gap-4">
						{icon && (
							<div className="shrink-0" aria-hidden="true">
								{icon}
							</div>
						)}
						<div>
							<h1 className="text-2xl font-bold text-white sm:text-3xl">
								{title}
							</h1>
							{subtitle && (
								<p className="mt-0.5 text-sm text-brand-100">{subtitle}</p>
							)}
						</div>
					</div>
					{actions && <div className="shrink-0">{actions}</div>}
				</div>

				{children && <div className="mt-5">{children}</div>}
			</div>

			<div
				className="absolute bottom-0 left-0 right-0 overflow-hidden leading-none"
				aria-hidden="true"
			>
				<svg
					viewBox="0 0 1440 40"
					fill="none"
					xmlns="http://www.w3.org/2000/svg"
					preserveAspectRatio="none"
					className="w-full text-white"
				>
					<path
						fill="currentColor"
						d="M0,0 C360,80 1080,80 1440,0 L1440,40 L0,40 Z"
					/>
				</svg>
			</div>
		</section>
	);
}
