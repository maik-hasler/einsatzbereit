import {
	AcademicCapIcon,
	ComputerDesktopIcon,
	GlobeIcon,
	HeartIcon,
	MusicalNoteIcon,
	PawIcon,
	ShieldCheckIcon,
	SparklesIcon,
	TrophyIcon,
	UserGroupIcon,
} from "../icons";

// Decorative banner icon shown on an opportunity card when it has no
// uploaded banner image, mapped from the opportunity's category.
export function CategoryGlyph({
	category,
	className = "h-10 w-10",
}: {
	category: string | undefined;
	className?: string;
}) {
	switch (category) {
		case "Social":
			return <UserGroupIcon className={className} />;
		case "Environment":
			return <GlobeIcon className={className} />;
		case "Sport":
			return <TrophyIcon className={className} />;
		case "Education":
			return <AcademicCapIcon className={className} />;
		case "DisasterRelief":
			return <ShieldCheckIcon className={className} />;
		case "Health":
			return <HeartIcon className={className} />;
		case "Animals":
			return <PawIcon className={className} />;
		case "Culture":
			return <MusicalNoteIcon className={className} />;
		case "Technology":
			return <ComputerDesktopIcon className={className} />;
		default:
			return <SparklesIcon className={className} />;
	}
}
