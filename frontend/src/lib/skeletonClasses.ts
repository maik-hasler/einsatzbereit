// Tailwind emits every `rounded-*` utility into the same layer at the same
// specificity, so a caller's `rounded-full` does not beat Skeleton's own
// `rounded-md` - stylesheet order decides, and it resolves to the 6px default.
// Every caller asking for a round placeholder silently got a rounded square,
// including the organization directory's and profile's avatar skeletons, which
// stand in for fully round avatars (#2331). Matching the caller's own radius
// (variant prefixes included, e.g. `sm:rounded-full`) is what lets the default
// step aside instead.
const RADIUS_UTILITY = /(?:^|\s)(?:[\w-]+:)*-?rounded(?:-|\s|$)/;

export function hasRadiusUtility(className: string): boolean {
	return RADIUS_UTILITY.test(className);
}

export function skeletonClass(className: string): string {
	const base = hasRadiusUtility(className)
		? "animate-pulse bg-gray-200 motion-reduce:animate-none"
		: "animate-pulse rounded-md bg-gray-200 motion-reduce:animate-none";

	return className ? `${base} ${className}` : base;
}
