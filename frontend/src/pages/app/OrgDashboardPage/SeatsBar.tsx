import type { OpportunityCapacity } from "../../../lib/opportunityCapacity";

// Above this many seats the ticks stop being countable and start being noise,
// so the meter switches to a single proportional bar.
const MAX_TICKS = 10;

/**
 * How full one occurrence is, drawn as the seats it has: a filled tick per
 * person signed up, a hollow one per place still open.
 *
 * This is the one fact the whole product turns on - a shift has places and some
 * of them are taken - and it is the only thing on the board drawn rather than
 * written. Reading down a list of six occurrences, the empty Saturday is the
 * row of hollow ticks; nobody has to compare "2/8" against "7/8" to find it.
 *
 * Decoration only: the count beside it says the same thing in words, so this is
 * `aria-hidden` and carries no colour-only meaning (WCAG 1.4.1).
 */
export default function SeatsBar({
	capacity,
	className = "",
}: {
	capacity: OpportunityCapacity;
	className?: string;
}) {
	// An uncapped occurrence has no seats to draw, and one with no places at all
	// is not being staffed here. Inventing a meter for either would be a picture
	// of something that does not exist.
	if (capacity.kind !== "capped") return null;

	const { booked, max } = capacity;
	const filled = Math.min(booked, max);

	if (max > MAX_TICKS) {
		return (
			<div
				aria-hidden="true"
				className={`h-1.5 w-16 overflow-hidden rounded-full bg-gray-200 ${className}`}
			>
				<div
					className="h-full rounded-full bg-brand-700"
					style={{ width: `${Math.round((filled / max) * 100)}%` }}
				/>
			</div>
		);
	}

	return (
		<div
			aria-hidden="true"
			className={`flex items-center gap-0.5 ${className}`}
		>
			{Array.from({ length: max }, (_, i) => (
				<span
					key={i}
					className={`h-1.5 w-2 rounded-full ${i < filled ? "bg-brand-700" : "bg-gray-200"}`}
				/>
			))}
		</div>
	);
}
