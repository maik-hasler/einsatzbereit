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
