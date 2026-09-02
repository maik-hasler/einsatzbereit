import { Children, type ReactNode } from "react";

/**
 * The panel's status strip is the one coloured surface below the header band,
 * so its tone has to carry information rather than decorate: whether a visitor
 * can still take part, and how urgently.
 */
export type PanelStatusTone = "open" | "urgent" | "closed";

const TONE_CLASSES: Record<PanelStatusTone, string> = {
	open: "bg-brand-700 text-white",

	// Tailwind's amber, not `accent-400`: the accent token is decoration only,
	// and anything that means "warning" belongs on the amber scale that
	// `WarningBanner` and `Chip`'s warning tone already use (frontend/AGENTS.md).
	// A step darker than those two, because this is a filled strip rather than
	// a tinted box - amber-900 on amber-100 reads 8.5:1.
	urgent: "bg-amber-100 text-amber-900",

	closed: "bg-gray-100 text-gray-700",
};

export interface PanelFact {
	key: string;
	icon: ReactNode;
	label: string;
	value: ReactNode;
	"data-testid"?: string;
}

interface Props {
	/** Rendered in the strip. `note` is the quieter second line under it. */
	status: { label: string; tone: PanelStatusTone; note?: string };

	facts: PanelFact[];

	/** The call to action, sign-in prompt, or owner notice for this visitor. */
	children?: ReactNode;

	"data-testid"?: string;
}

/**
 * The one thing a visitor came to decide - can I take part, when, and where -
 * next to the button that acts on it. It replaces an "at a glance" band that
 * restated the schedule and location sections immediately below it (#2330).
 */
export default function OpportunityActionPanel({
	status,
	facts,
	children,
	"data-testid": testId,
}: Props) {
	return (
		<div
			data-testid={testId}
			className="overflow-hidden rounded-card border border-gray-100 bg-white shadow-resting"
		>
			<p
				data-testid="opportunity-capacity"
				className={`px-5 py-3 text-sm font-semibold ${TONE_CLASSES[status.tone]}`}
			>
				{status.label}
				{status.note && (
					<span
						data-testid="opportunity-capacity-secondary"
						className="mt-0.5 block text-xs font-normal opacity-90"
					>
						{status.note}
					</span>
				)}
			</p>

			{/* The icon belongs inside the <dt>, not in a sibling column: axe's
			`dlitem` rule wants a <dt>/<dd> one level below the <dl> at most, and a
			wrapper column puts them two deep. */}
			<dl className="divide-y divide-gray-100 px-5">
				{facts.map((fact) => (
					<div key={fact.key} className="py-3">
						<dt className="flex items-center gap-2 text-xs font-semibold tracking-widest text-gray-500 uppercase">
							<span className="shrink-0 text-brand-700">{fact.icon}</span>
							{fact.label}
						</dt>
						<dd
							className="mt-1 text-sm font-medium text-gray-900"
							data-testid={fact["data-testid"]}
						>
							{fact.value}
						</dd>
					</div>
				))}
			</dl>

			{/* `Children.toArray` rather than a truthiness check: the call site
			passes two conditional slots (`{cta}{signInPrompt}`), and React hands
			those over as an array that is truthy even when both conditions are
			false - which is exactly the owner viewing their own published
			opportunity, who would otherwise get an empty padded strip. */}
			{Children.toArray(children).length > 0 && (
				<div className="space-y-3 border-t border-gray-100 px-5 py-4">
					{children}
				</div>
			)}
		</div>
	);
}
