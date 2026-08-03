// Notifies listeners that the signed-in user's avatar changed (uploaded via
// ProfileOverviewPage) so the header's independently-fetched copy
// (useAccountMenu) can refresh itself instead of showing the pre-upload
// image until the next full reload (#1245). Same minimal pub/sub shape as
// sessionExpiryBus.ts.
type Listener = () => void;

const listeners: Listener[] = [];

export function subscribeAvatarChanged(listener: Listener): () => void {
	listeners.push(listener);
	return () => {
		const idx = listeners.indexOf(listener);
		if (idx !== -1) listeners.splice(idx, 1);
	};
}

export function notifyAvatarChanged(): void {
	listeners.forEach((l) => l());
}
