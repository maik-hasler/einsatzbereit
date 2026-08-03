import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { StarIcon } from "./icons";
import { labelClass } from "../lib/formClasses";

interface SubmitFeedbackModalProps {
	engagementId: string;
	opportunityTitle: string;
	onSubmitted: () => void;
	onClose: () => void;
}

export default function SubmitFeedbackModal({
	engagementId,
	opportunityTitle,
	onSubmitted,
	onClose,
}: SubmitFeedbackModalProps) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [rating, setRating] = useState(0);
	const [hovered, setHovered] = useState(0);
	const [comment, setComment] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		if (rating === 0) return;
		setSubmitting(true);
		setError(null);
		try {
			await api.submitFeedback(engagementId, {
				rating,
				comment: comment.trim() || undefined,
			});
			onSubmitted();
			onClose();
		} catch {
			setError(t("feedback.submitError"));
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
				{t("feedback.title")}
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
						className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-500"
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
						{submitting ? t("feedback.submitting") : t("feedback.submit")}
					</Button>
				</div>
			</form>
		</Modal>
	);
}
