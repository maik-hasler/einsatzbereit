import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { TimeSlotDetail } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import {
	computeSpotsLeft,
	formatDateTimeRange,
	isSlotFull,
} from "../lib/format";
import { getApiErrorMessage, isApiErrorCode } from "../lib/apiError";
import { labelClass, textareaClass } from "../lib/formClasses";
import Dropdown from "./Dropdown";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { RequiredFieldsLegend, RequiredMark } from "./RequiredMark";

interface Props {
	opportunityId: string;
	organizationId: string;
	participationType: string;
	timeSlots: TimeSlotDetail[];
	// Set when the volunteer clicked a specific slot row on the detail page
	// rather than the rail's generic sign-up button (#2075) - that click
	// already answered "which slot", so this skips the redundant re-picking
	// step below and goes straight to a confirmation of that one slot.
	preselectedTimeSlotId?: string;
	onClose: () => void;
	onSuccess: () => void;
}

export default function SignUpModal({
	opportunityId,
	organizationId,
	participationType,
	timeSlots,
	preselectedTimeSlotId,
	onClose,
	onSuccess,
}: Props) {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	// Only a row click settles which slot up front - the rail button is a
	// generic "sign up" trigger with no slot of its own in mind, so it always
	// goes through the picker below just as before (pre-selected once there's
	// only one open slot to pick, #657 - not skipped, since the rail button
	// remains a secondary entry point without a slot already in hand, #2075).
	const confirmedTimeSlot = preselectedTimeSlotId
		? timeSlots.find((ts) => ts.id === preselectedTimeSlotId)
		: undefined;
	const [selectedTimeSlotId, setSelectedTimeSlotId] = useState<string>(() => {
		if (confirmedTimeSlot) return confirmedTimeSlot.id;
		const availableSlots = timeSlots.filter(
			(ts) => !isSlotFull(ts.maxParticipants, ts.bookedCount),
		);
		return availableSlots.length === 1 ? availableSlots[0].id : "";
	});
	const [message, setMessage] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);
	// Set alongside `error` (never independently) when the failure is
	// specifically the reactivation churn limit (Engagement.Reactivate,
	// backend/src/Domain/Engagements/Engagement.cs) - the error message names
	// "the organization" as the remedy but has no way to link to it on its
	// own, so this renders an actual link below it instead (#2043).
	const [showContactOrganizationLink, setShowContactOrganizationLink] =
		useState(false);
	const [messageError, setMessageError] = useState<string | null>(null);
	const messageFieldRef = useRef<HTMLTextAreaElement>(null);

	const isScheduledSlots = participationType === "ScheduledSlots";

	// Native `required` used to move focus to the invalid field for free as
	// part of the browser's own constraint validation - #1908 replaced that
	// validation with a translated inline message, so focus has to be sent
	// there by hand (mirrors DetailsStep.tsx's error-focus effect, whose
	// equivalent is react-hook-form's default `shouldFocusError` elsewhere).
	useEffect(() => {
		if (!messageError) return;
		messageFieldRef.current?.focus();
	}, [messageError]);

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		setError(null);
		setShowContactOrganizationLink(false);
		if (isScheduledSlots && timeSlots.length > 0 && !selectedTimeSlotId) {
			setError(t("signUp.selectTimeSlotRequired"));
			return;
		}
		if (!isScheduledSlots && !message.trim()) {
			setMessageError(t("signUp.messageRequired"));
			return;
		}
		setMessageError(null);
		setSubmitting(true);

		try {
			await api.createEngagement(opportunityId, {
				type: isScheduledSlots ? "ScheduledSlots" : "IndividualContact",
				timeSlotId:
					isScheduledSlots && selectedTimeSlotId
						? selectedTimeSlotId
						: undefined,
				message: !isScheduledSlots ? message : undefined,
			});
			onSuccess();
			onClose();
		} catch (err) {
			setError(getApiErrorMessage(err, t("signUp.unknownError")));
			setShowContactOrganizationLink(
				isApiErrorCode(err, "Engagement.ReactivationLimitReached"),
			);
		} finally {
			setSubmitting(false);
		}
	}

	return (
		<Modal
			onClose={onClose}
			labelledBy="sign-up-dialog-title"
			maxWidth="max-w-md"
		>
			<h2 id="sign-up-dialog-title" className="mb-4 text-lg font-semibold">
				{isScheduledSlots
					? confirmedTimeSlot
						? t("signUp.titleConfirm")
						: t("signUp.titleWaitlist")
					: t("signUp.titleInterest")}
			</h2>

			<form onSubmit={handleSubmit} className="space-y-4">
				{isScheduledSlots && (
					<div>
						{timeSlots.length === 0 ? (
							<p className="text-sm text-gray-500">{t("signUp.noTimeSlots")}</p>
						) : confirmedTimeSlot ? (
							// The volunteer already named this exact slot by clicking its
							// row, so this states it as a fact instead of making them
							// re-pick it from the same dropdown they'd otherwise see below
							// (#2075).
							<p
								className="text-sm text-gray-700"
								data-testid="sign-up-confirmed-slot"
							>
								{t("signUp.confirmTimeSlot", {
									range: formatDateTimeRange(
										confirmedTimeSlot.startDateTime as unknown as string,
										confirmedTimeSlot.endDateTime as unknown as string,
										i18n.language,
									),
								})}
							</p>
						) : (
							<>
								{/* Visually hidden, not removed - the dialog title just above
								("Sign up for a slot") already conveys this on screen, so showing
								it again here read as a duplicated label (#987); the dropdown
								still needs its own accessible name for screen reader users
								landing on it directly, shortened to the field's name alone so it
								doesn't just restate the title (#2056). */}
								<label htmlFor="sign-up-time-slot" className="sr-only">
									{t("signUp.selectTimeSlot")}
								</label>
								<Dropdown
									id="sign-up-time-slot"
									value={selectedTimeSlotId}
									onChange={setSelectedTimeSlotId}
									placeholder={t("signUp.selectPlaceholder")}
									className="mt-1 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400"
									options={timeSlots.map((ts) => {
										const spotsLeft = computeSpotsLeft(
											ts.maxParticipants,
											ts.bookedCount,
										);
										const slotFull = isSlotFull(
											ts.maxParticipants,
											ts.bookedCount,
										);
										return {
											value: ts.id,
											disabled: slotFull,
											label: `${formatDateTimeRange(
												ts.startDateTime as unknown as string,
												ts.endDateTime as unknown as string,
												i18n.language,
											)} · ${
												spotsLeft === null
													? t("opportunities.unlimitedSpots")
													: slotFull
														? t("opportunities.full")
														: t("opportunities.spotsLeft", { count: spotsLeft })
											}`,
										};
									})}
								/>
							</>
						)}
					</div>
				)}

				{!isScheduledSlots && (
					<div>
						{/* Scoped to this branch: the slot-picker variant has no required
						field, so its legend would explain an absent asterisk. */}
						<RequiredFieldsLegend className="mb-2" />
						<label htmlFor="sign-up-message" className={`mb-1 ${labelClass}`}>
							{t("signUp.message")}
							<RequiredMark />
						</label>
						<textarea
							id="sign-up-message"
							ref={messageFieldRef}
							value={message}
							onChange={(e) => {
								setMessage(e.target.value);
								if (messageError) setMessageError(null);
							}}
							aria-required="true"
							aria-invalid={messageError ? true : undefined}
							aria-describedby={
								messageError ? "sign-up-message-error" : undefined
							}
							rows={4}
							placeholder={t("signUp.messagePlaceholder")}
							className={textareaClass}
						/>
						{messageError && (
							<p
								id="sign-up-message-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{messageError}
							</p>
						)}
					</div>
				)}

				{error && (
					<>
						<ErrorBanner message={error} />
						{showContactOrganizationLink && (
							<Link
								to={`/organizations/${organizationId}`}
								className="mt-1 inline-block text-sm text-brand-700 underline-offset-2 hover:text-brand-800 hover:underline"
							>
								{t("common.contactOrganization")}
							</Link>
						)}
					</>
				)}

				<div className="flex justify-end gap-2">
					<Button type="button" variant="secondary" onClick={onClose}>
						{t("signUp.cancel")}
					</Button>
					<Button
						type="submit"
						disabled={
							submitting || (isScheduledSlots && timeSlots.length === 0)
						}
					>
						{submitting
							? t("signUp.submitting")
							: isScheduledSlots
								? t("signUp.submitWaitlist")
								: t("signUp.submitInterest")}
					</Button>
				</div>
			</form>
		</Modal>
	);
}
