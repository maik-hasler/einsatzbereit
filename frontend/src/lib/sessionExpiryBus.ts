type Listener = () => void;

const listeners: Listener[] = [];

export function subscribeSessionExpired(listener: Listener): () => void {
	listeners.push(listener);
	return () => {
		const idx = listeners.indexOf(listener);
		if (idx !== -1) listeners.splice(idx, 1);
	};
}

export function notifySessionExpired(): void {
	listeners.forEach((l) => l());
}
