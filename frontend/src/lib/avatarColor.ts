// Fixed palette of Tailwind bg/text pairs (same 100/700 shade combo the
// initial-avatar already used, just varied by hue) - deterministic per seed
// so the same organization always gets the same color, and same-initial
// organizations (e.g. two names both starting with "F") are no longer
// visually indistinguishable in the directory (#993).
const AVATAR_COLOR_PALETTE = [
	{ bg: "bg-brand-100", text: "text-brand-700" },
	{ bg: "bg-blue-100", text: "text-blue-700" },
	{ bg: "bg-purple-100", text: "text-purple-700" },
	{ bg: "bg-amber-100", text: "text-amber-700" },
	{ bg: "bg-teal-100", text: "text-teal-700" },
	{ bg: "bg-rose-100", text: "text-rose-700" },
] as const;

export interface AvatarColorClasses {
	bg: string;
	text: string;
}

/** Deterministically picks a color pair from a fixed palette based on `seed`
 * (e.g. an organization id) - same seed always maps to the same pair. */
export function avatarColorClasses(seed: string): AvatarColorClasses {
	let hash = 0;
	for (let i = 0; i < seed.length; i++) {
		hash = (hash * 31 + seed.charCodeAt(i)) | 0;
	}
	const index = Math.abs(hash) % AVATAR_COLOR_PALETTE.length;
	return AVATAR_COLOR_PALETTE[index];
}
