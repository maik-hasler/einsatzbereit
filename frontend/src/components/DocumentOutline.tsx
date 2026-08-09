import { useEffect, useState } from "react";

export interface OutlineEntry {
	/** Must match the id on the corresponding <DocumentSection>. */
	id: string;
	label: string;
}

interface Props {
	entries: OutlineEntry[];
	/** Accessible name for the nav landmark, e.g. "On this page". */
	label: string;
}

// Sticky table of contents for the long legal texts. The privacy policy runs
// to nine sections and the terms to seven; as flat prose they were only
// readable front-to-back, with no way to see the shape of the document or jump
// to the one clause a visitor actually came for (issue #1755). Legal text is
// genuinely a numbered clause sequence, so an outline encodes real structure
// here rather than decorating a list.
//
// Scroll position is tracked with IntersectionObserver rather than a scroll
// handler so the browser does the work off the main thread. rootMargin's -96px
// top inset keeps the sticky site header from claiming a section before it is
// actually in view; the -70% bottom inset narrows the "active" strip to the
// upper third of the viewport, so exactly one entry lights up at a time
// instead of every section that happens to be on screen.
export default function DocumentOutline({ entries, label }: Props) {
	const [activeId, setActiveId] = useState<string | undefined>(entries[0]?.id);

	// Derived string, not the array itself: the caller writes `entries` inline,
	// so a new array identity arrives on every render and would re-run this
	// effect (tearing down and rebuilding the observer) each time. Joining the
	// ids gives a value that only changes when the outline really changes, and
	// keeps the effect honest for exhaustive-deps - nothing else is referenced.
	const ids = entries.map((entry) => entry.id).join(",");

	useEffect(() => {
		const observer = new IntersectionObserver(
			(observed) => {
				const visible = observed.filter((entry) => entry.isIntersecting);
				if (visible.length === 0) return;
				const topmost = visible.reduce((a, b) =>
					a.boundingClientRect.top <= b.boundingClientRect.top ? a : b,
				);
				setActiveId(topmost.target.id);
			},
			{ rootMargin: "-96px 0px -70% 0px" },
		);

		for (const id of ids.split(",")) {
			const element = document.getElementById(id);
			if (element) observer.observe(element);
		}

		return () => observer.disconnect();
	}, [ids]);

	return (
		<nav aria-label={label} className="lg:sticky lg:top-24 lg:self-start">
			<p className="mb-4 text-xs font-semibold tracking-widest text-gray-600 uppercase">
				{label}
			</p>
			<ol className="space-y-1 border-l border-gray-200">
				{entries.map((entry, index) => {
					const isActive = entry.id === activeId;
					return (
						<li key={entry.id}>
							<a
								href={`#${entry.id}`}
								aria-current={isActive ? "true" : undefined}
								className={`-ml-px flex gap-3 border-l-2 py-1.5 pr-2 pl-4 text-sm transition-colors ${
									isActive
										? "border-brand-700 font-semibold text-brand-700"
										: "border-transparent text-gray-600 hover:border-gray-300 hover:text-gray-900"
								}`}
							>
								<span aria-hidden="true" className="font-display tabular-nums">
									{index + 1}
								</span>
								<span className="min-w-0">{entry.label}</span>
							</a>
						</li>
					);
				})}
			</ol>
		</nav>
	);
}
