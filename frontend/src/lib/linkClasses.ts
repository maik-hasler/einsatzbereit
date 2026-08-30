// The one treatment for a link sitting inside a block of prose. Colour alone is
// not a distinguishing cue: the green tint against the surrounding grey text
// measures 1.37:1, well under WCAG 1.4.1's 3:1 floor, so the link has to carry
// a rest-state underline (#2327). Five static pages had already settled on
// exactly this recipe and re-declared it locally; the copies that had not -
// notably "Contact the organization" on /my-signups - were the ones axe
// reported as `link-in-text-block`.
export const inlineLinkClass =
	"font-medium text-brand-700 underline underline-offset-2 hover:text-brand-800";
