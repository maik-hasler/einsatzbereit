import { addDays } from "date-fns";

// Mirrors Engagement.FeedbackEditWindowDays on the backend (#1069) - only
// used here to decide whether to show the Edit/Delete affordances at all.
// The backend re-checks this independently on every request, so a stale or
// skewed client clock can only ever hide the buttons a little early/late,
// never bypass the real guard.
export const FEEDBACK_EDIT_WINDOW_DAYS = 14;

export function isFeedbackEditable(
	feedbackSubmittedAt: Date | string | null | undefined,
	now: Date = new Date(),
): boolean {
	if (!feedbackSubmittedAt) return false;
	const submittedAt = new Date(feedbackSubmittedAt);
	return now <= addDays(submittedAt, FEEDBACK_EDIT_WINDOW_DAYS);
}
