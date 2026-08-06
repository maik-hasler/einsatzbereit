// Fallback banner gradient shown on an opportunity card when it has no
// uploaded banner image, varied by category so a list of fallback cards
// doesn't read as one undifferentiated block of color (paired with the
// per-category icon in VolunteerOpportunitiesList/icons.tsx).
export const OPPORTUNITY_CATEGORY_BANNER_CLASSES: Record<string, string> = {
	Social: "bg-gradient-to-br from-rose-500 to-rose-800",
	Environment: "bg-gradient-to-br from-emerald-500 to-emerald-800",
	Sport: "bg-gradient-to-br from-orange-500 to-orange-800",
	Education: "bg-gradient-to-br from-sky-500 to-sky-800",
	DisasterRelief: "bg-gradient-to-br from-red-500 to-red-800",
	Health: "bg-gradient-to-br from-pink-500 to-pink-800",
	Animals: "bg-gradient-to-br from-amber-500 to-amber-800",
	Culture: "bg-gradient-to-br from-violet-500 to-violet-800",
	Technology: "bg-gradient-to-br from-indigo-500 to-indigo-800",
};

export const OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS =
	"bg-gradient-to-br from-brand-500 to-brand-800";

// White label text alone fails WCAG AA against several of the gradients above
// (worst case ~2.1:1 on amber-500, einsatzbereit#1671) - a black/40 scrim
// strip behind the text clears 4.5:1 against every category's lightest
// ("from") stop with margin (worst case ~5.4:1 on amber-500), so the fix
// works for any gradient here without per-category tuning. The class must
// stay a literal string for Tailwind's content scanner to pick it up; keep
// the opacity constant below in sync with it by hand.
export const OPPORTUNITY_CATEGORY_LABEL_SCRIM_CLASS = "bg-black/40";
export const OPPORTUNITY_CATEGORY_LABEL_SCRIM_OPACITY = 0.4;

export function getOpportunityCategoryBannerClassName(
	category: string | undefined,
): string {
	if (!category) return OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS;
	return (
		OPPORTUNITY_CATEGORY_BANNER_CLASSES[category] ??
		OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS
	);
}
