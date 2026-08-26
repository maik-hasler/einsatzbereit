import { useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import { textareaClass } from "../lib/formClasses";
import { getApiErrorMessage } from "../lib/apiError";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { StarIcon } from "./icons";
import { RequiredFieldsLegend, RequiredMark } from "./RequiredMark";
import { labelClass } from "../lib/formClasses";

const STAR_VALUES = [1, 2, 3, 4, 5];

interface SubmitFeedbackModalProps {
	engagementId: string;
	opportunityTitle: string;
	onSubmitted: (rating: number, comment: string | null) => void;
	onClose: () => void;

	initialRating?: number;
	initialComment?: string | null;
}

export default function SubmitFeedbackModal({
	engagementId,
	opportunityTitle,
	onSubmitted,
	onClose,
	initialRating,
	initialComment,
}: SubmitFeedbackModalProps) {
	const api = useApiClient();
	const { t } = useTranslation();
	const isEditing = initialRating !== undefined;
	const [rating, setRating] = useState(initialRating ?? 0);
	const [hovered, setHovered] = useState(0);
	const [comment, setComment] = useState(initialComment ?? "");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const starRefs = useRef<(HTMLButtonElement | null)[]>([]);

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		if (rating === 0) return;
		setSubmitting(true);
		setError(null);
		const trimmedComment = comment.trim() || null;
		try {
			if (isEditing) {
				await api.updateFeedback(engagementId, {
					rating,
					comment: trimmedComment ?? undefined,
				});
			} else {
				await api.submitFeedback(engagementId, {
					rating,
					comment: trimmedComment ?? undefined,
				});
			}
			onSubmitted(rating, trimmedComment);
			onClose();
		} catch (err) {
			setError(getApiErrorMessage(err, t("feedback.submitError")));
		} finally {
			setSubmitting(false);
		}
	}

	const displayRating = hovered || rating;

	function moveRating(delta: number) {
		const current = rating || 1;
		const next =
			((current - 1 + delta + STAR_VALUES.length) % STAR_VALUES.length) + 1;
		setRating(next);
		starRefs.current[next - 1]?.focus();
	}

	function handleRatingKeyDown(e: ReactKeyboardEvent<HTMLButtonElement>) {
		switch (e.key) {
			case "ArrowRight":
			case "ArrowUp":
				e.preventDefault();
				moveRating(1);
				break;
			case "ArrowLeft":
			case "ArrowDown":
				e.preventDefault();
				moveRating(-1);
				break;
			case "Home":
				e.preventDefault();
				setRating(1);
				starRefs.current[0]?.focus();
				break;
			case "End":
				e.preventDefault();
				setRating(STAR_VALUES.length);
				starRefs.current[STAR_VALUES.length - 1]?.focus();
				break;
			default:
				break;
		}
	}

	return (
		<Modal onClose={onClose} labelledBy="feedback-title" maxWidth="max-w-md">
			<h2
				id="feedback-title"
				className="mb-1 text-lg font-semibold text-gray-900"
			>
				{isEditing ? t("feedback.editTitle") : t("feedback.title")}
			</h2>
			<p className="mb-5 text-sm text-gray-500">{opportunityTitle}</p>

			<form onSubmit={(e) => void handleSubmit(e)} className="space-y-5">
				<RequiredFieldsLegend />

				<div>
					<p className={`mb-2 ${labelClass}`}>
						{t("feedback.ratingLabel")}
						<RequiredMark />
					</p>

					<div
						className="flex gap-1"
						role="radiogroup"
						aria-label={`${t("feedback.ratingLabel")} (${t("common.requiredField")})`}
					>
						{STAR_VALUES.map((star) => (
							<button
								key={star}
								ref={(el) => {
									starRefs.current[star - 1] = el;
								}}
								type="button"
								role="radio"
								aria-checked={rating === star}
								aria-label={t("feedback.starLabel", { count: star })}
								tabIndex={star === (rating || 1) ? 0 : -1}
								onClick={() => setRating(star)}
								onKeyDown={handleRatingKeyDown}
								onMouseEnter={() => setHovered(star)}
								onMouseLeave={() => setHovered(0)}
								className="rounded"
							>
								<StarIcon
									className={`h-8 w-8 transition-colors ${star <= displayRating ? "text-yellow-700" : "text-gray-500"}`}
								/>
							</button>
						))}
					</div>
				</div>

				<div>
					<label htmlFor="feedback-comment" className={labelClass}>
						{t("feedback.commentLabel")}
					</label>
					<textarea
						id="feedback-comment"
						rows={3}
						maxLength={500}
						value={comment}
						onChange={(e) => setComment(e.target.value)}
						placeholder={t("feedback.commentPlaceholder")}
						className={textareaClass}
					/>
					<p className="mt-1 text-right text-xs text-gray-500">
						{comment.length}/500
					</p>
				</div>

				{error && <ErrorBanner message={error} />}

				<div className="flex gap-3">
					<Button
						type="button"
						variant="secondary"
						onClick={onClose}
						className="flex-1"
					>
						{t("feedback.cancel")}
					</Button>
					<Button
						type="submit"
						disabled={submitting || rating === 0}
						className="flex-1"
					>
						{submitting
							? t("feedback.submitting")
							: isEditing
								? t("feedback.saveChanges")
								: t("feedback.submit")}
					</Button>
				</div>
			</form>
		</Modal>
	);
}
