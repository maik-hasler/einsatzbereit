import { describe, it, expect } from "vitest";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import OpportunityResultsList from "./OpportunityResultsList";
import { renderWithProviders } from "../../test/render";

const baseProps = {
	loading: false,
	error: null as string | null,
	errorIsOffline: false,
	items: [] as VolunteerOpportunitySummary[],
	totalItems: undefined as number | undefined,
	hasFilters: false,
	onClearFilters: () => {},
	hasMore: false,
	loadingMore: false,
	onLoadMore: () => {},
	loadMoreError: null as string | null,
	loadMoreErrorIsOffline: false,
	onRetryLoadMore: () => {},
	pageSize: 9,
};

// The loading grid stood in for a page of results with three placeholders,
// whatever the page actually held - nine cards at xl - so the grid re-laid
// itself out the moment the results landed (#2329 F6).
describe("OpportunityResultsList loading placeholders", () => {
	it.each([
		[9, 9],
		[8, 8],
		[5, 5],
	])(
		"draws one placeholder per card a %i-card page holds",
		(pageSize, want) => {
			const { container } = renderWithProviders(
				<OpportunityResultsList
					{...baseProps}
					loading
					pageSize={pageSize as number}
				/>,
			);

			expect(container.querySelectorAll('[role="status"] > div')).toHaveLength(
				want as number,
			);
		},
	);

	it("keeps the placeholders out of the accessibility tree", () => {
		const { container } = renderWithProviders(
			<OpportunityResultsList {...baseProps} loading />,
		);

		for (const card of container.querySelectorAll('[role="status"] > div')) {
			expect(card).toHaveAttribute("aria-hidden", "true");
		}
	});
});
