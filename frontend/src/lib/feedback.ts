import { addDays } from "date-fns";

// Mirrors Engagement.FeedbackEditWindowDays on the backend (#1069) - only
// used here to decide whether to show the Edit/Delete affordances at all.
// The backend re-checks this independently on every request, so a stale or
// skewed client clock can only ever hide the buttons a little early/late,
// never bypass the real guard.
export const FEEDBACK_EDIT_WINDOW_DAYS = 14;

export function getFeedbackEditDeadline(
	feedbackSubmittedAt: Date | string | null | undefined,
): Date | null {
	if (!feedbackSubmittedAt) return null;
	return addDays(new Date(feedbackSubmittedAt), FEEDBACK_EDIT_WINDOW_DAYS);
}

export function isFeedbackEditable(
	feedbackSubmittedAt: Date | string | null | undefined,
	now: Date = new Date(),
): boolean {
	const deadline = getFeedbackEditDeadline(feedbackSubmittedAt);
	return deadline !== null && now <= deadline;
}
