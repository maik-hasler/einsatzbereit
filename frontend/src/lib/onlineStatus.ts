type Listener = () => void;

export function subscribeOnlineStatus(listener: Listener): () => void {
	window.addEventListener("online", listener);
	window.addEventListener("offline", listener);
	return () => {
		window.removeEventListener("online", listener);
		window.removeEventListener("offline", listener);
	};
}

export function getOnlineStatus(): boolean {
	return navigator.onLine;
}
