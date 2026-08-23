import type { ReactNode } from "react";

interface Props {
	id: string;

	number?: number;
	title: string;
	children: ReactNode;
}

export default function DocumentSection({
	id,
	number,
	title,
	children,
}: Props) {
	return (
		<section
			id={id}
			aria-labelledby={`${id}-title`}
			className="scroll-mt-28 border-t border-gray-200 pt-10 first:border-t-0 first:pt-0"
		>
			<h2 id={`${id}-title`} className="font-display text-gray-900">
				{number !== undefined && (
					<span className="block text-2xl font-bold text-brand-700 tabular-nums">
						{number}
					</span>
				)}
				<span className="mt-1 block text-3xl font-bold sm:text-4xl">
					{title}
				</span>
			</h2>
			<div className="mt-5 space-y-4 text-base leading-7 text-gray-700">
				{children}
			</div>
		</section>
	);
}
