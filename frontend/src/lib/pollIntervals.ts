/**
 * How often the two background pollers on every authenticated page ask again.
 *
 * One value rather than two literals: `useAccountMenu` (unread notification
 * count) and `useAchievementNotifier` (newly earned badges) run side by side on
 * the same page and have always used the same minute. Naming it keeps that a
 * decision instead of a coincidence, and keeps the two from drifting into
 * requesting at different offsets.
 *
 * Both pause while the tab is hidden - TanStack Query's `refetchInterval` does
 * not run in the background unless asked to.
 */
export const NOTIFICATION_POLL_INTERVAL_MS = 60_000;
