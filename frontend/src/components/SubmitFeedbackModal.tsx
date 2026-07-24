import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";

interface SubmitFeedbackModalProps {
	engagementId: string;
	opportunityTitle: string;
	onSubmitted: () => void;
	onClose: () => void;
}

function StarIcon({ filled }: { filled: boolean }) {
	return (
		<svg
			className={`h-8 w-8 transition-colors ${filled ? "text-yellow-400" : "text-gray-300"}`}
			fill="currentColor"
			viewBox="0 0 24 24"
			aria-hidden="true"
		>
			<path d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.006 5.404.434c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.434 2.082-5.005Z" />
		</svg>
	);
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
					<p className="mb-2 text-sm font-medium text-gray-700">
						{t("feedback.ratingLabel")}
					</p>
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
								className="rounded focus:outline-none focus:ring-2 focus:ring-brand-500"
							>
								<StarIcon filled={star <= displayRating} />
							</button>
						))}
					</div>
				</div>

				<div>
					<label
						htmlFor="feedback-comment"
						className="block text-sm font-medium text-gray-700"
					>
						{t("feedback.commentLabel")}
					</label>
					<textarea
						id="feedback-comment"
						rows={3}
						maxLength={500}
						value={comment}
						onChange={(e) => setComment(e.target.value)}
						placeholder={t("feedback.commentPlaceholder")}
						className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
					/>
					<p className="mt-1 text-right text-xs text-gray-400">
						{comment.length}/500
					</p>
				</div>

				{error && <ErrorBanner message={error} />}

				<div className="flex gap-3">
					<button
						type="button"
						onClick={onClose}
						className="flex-1 rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
					>
						{t("feedback.cancel")}
					</button>
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
