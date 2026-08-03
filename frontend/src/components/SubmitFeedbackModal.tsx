import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import { textareaClass } from "../lib/formClasses";
import { getApiErrorMessage } from "../lib/apiError";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { StarIcon } from "./icons";
import { labelClass } from "../lib/formClasses";

interface SubmitFeedbackModalProps {
	engagementId: string;
	opportunityTitle: string;
	onSubmitted: (rating: number, comment: string | null) => void;
	onClose: () => void;
	/** Pre-fills the form and switches submit/cancel wording to editing an
	 * existing rating (PUT) instead of creating a new one (POST). */
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
				<div>
					<p className={`mb-2 ${labelClass}`}>{t("feedback.ratingLabel")}</p>
					<div
						className="flex gap-1"
						role="group"
						aria-label={t("feedback.ratingLabel")}
					>
						{[1, 2, 3, 4, 5].map((star) => (
							<button
								key={star}
								type="button"
								aria-label={t("feedback.starLabel", { count: star })}
								aria-pressed={rating === star}
								onClick={() => setRating(star)}
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
