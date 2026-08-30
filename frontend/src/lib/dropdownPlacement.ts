/**
 * Which side of its trigger a dropdown panel should open on.
 *
 * A panel anchored below its trigger runs off the bottom of a short viewport - the
 * browse page's date picker is ~302px tall and on a 390x844 phone opened ~50px past
 * the fold, hiding its last week row, the legend and the selected-range footer with
 * nothing to scroll them into view (#2319). Flip to the other side when it does not
 * fit below and there is more room above.
 */
export function resolveDropdownPlacement({
	triggerTop,
	triggerBottom,
	panelHeight,
	viewportHeight,
	edgeMargin,
}: {
	triggerTop: number;
	triggerBottom: number;
	panelHeight: number;
	viewportHeight: number;
	edgeMargin: number;
}): "above" | "below" {
	const spaceBelow = viewportHeight - triggerBottom - edgeMargin;
	const spaceAbove = triggerTop - edgeMargin;

	return panelHeight > spaceBelow && spaceAbove > spaceBelow
		? "above"
		: "below";
}
