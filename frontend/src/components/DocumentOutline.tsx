import { useEffect, useState } from "react";

export interface OutlineEntry {
	id: string;
	label: string;
}

interface Props {
	entries: OutlineEntry[];

	label: string;
}

export default function DocumentOutline({ entries, label }: Props) {
	const [activeId, setActiveId] = useState<string | undefined>(entries[0]?.id);

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
