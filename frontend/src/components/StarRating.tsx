import { useTranslation } from "react-i18next";
import { StarIcon } from "./icons";

const STAR_VALUES = [1, 2, 3, 4, 5];

interface Props {
	rating: number;
	className?: string;
	size?: "sm" | "md";
}

/** Read-only rendering of a submitted rating - the interactive picker lives in SubmitFeedbackModal. */
export default function StarRating({
	rating,
	className = "",
	size = "md",
}: Props) {
	const { t } = useTranslation();
	const starClass = size === "sm" ? "h-3.5 w-3.5" : "h-4 w-4";
	return (
		<span
			className={`inline-flex items-center gap-0.5 ${className}`}
			role="img"
			aria-label={t("feedback.itemRatingLabel", { rating })}
		>
			{STAR_VALUES.map((star) => (
				<StarIcon
					key={star}
					className={`${starClass} ${star <= rating ? "text-yellow-700" : "text-gray-500"}`}
				/>
			))}
		</span>
	);
}
