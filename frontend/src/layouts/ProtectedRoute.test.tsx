import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ProtectedRoute from "./ProtectedRoute";
import { renderWithProviders } from "../test/render";

describe("ProtectedRoute auth loading state (#2206)", () => {
	it("keeps rendering children while an already-authenticated user has a background token refresh in flight", () => {
		renderWithProviders(
			<ProtectedRoute>
				<div>Protected content</div>
			</ProtectedRoute>,
			{ auth: { isAuthenticated: true, isLoading: true } },
		);

		expect(screen.getByText("Protected content")).toBeInTheDocument();
		expect(screen.queryByRole("status")).toBeNull();
	});

	it("shows the loading spinner instead of children while auth state is still being determined", () => {
		renderWithProviders(
			<ProtectedRoute>
				<div>Protected content</div>
			</ProtectedRoute>,
			{ auth: { isAuthenticated: false, isLoading: true } },
		);

		expect(screen.queryByText("Protected content")).toBeNull();
		expect(screen.getByRole("status")).toHaveTextContent("Loading…");
	});
});
