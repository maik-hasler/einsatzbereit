import { useEffect } from "react";
import { registerPageDescription } from "../lib/documentMeta";

export function usePageDescription(description?: string | null) {
	useEffect(() => {
		// Nothing to say for this route - it falls back to the interface
		// language's default description rather than keeping whatever the
		// previous route left behind.
		if (!description) return;
		return registerPageDescription(description);
	}, [description]);
}
