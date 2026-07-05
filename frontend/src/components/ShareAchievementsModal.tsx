import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { QRCodeSVG } from "qrcode.react";
import { dispatchToast } from "../lib/toastBus";

interface Props {
	shareUrl: string;
	onClose: () => void;
}

export default function ShareAchievementsModal({ shareUrl, onClose }: Props) {
	const { t } = useTranslation();

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") onClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose]);

	function handleCopy() {
		void navigator.clipboard
			.writeText(shareUrl)
			.then(() => dispatchToast("success", t("achievements.shareCopied")));
	}

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center p-3 sm:p-4">
			<button
				type="button"
				className="absolute inset-0 bg-black/50"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="share-achievements-title"
				className="relative z-10 w-full max-w-sm rounded-xl bg-white p-6 shadow-xl"
			>
				<div className="flex items-center justify-between">
					<h2
						id="share-achievements-title"
						className="text-lg font-semibold text-gray-900"
					>
						{t("achievements.shareButton")}
					</h2>
					<button
						type="button"
						onClick={onClose}
						className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
						aria-label={t("achievements.shareClose")}
					>
						<svg
							className="h-5 w-5"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M6 18 18 6M6 6l12 12"
							/>
						</svg>
					</button>
				</div>

				<p className="mt-2 text-sm text-gray-600">
					{t("achievements.shareText")}
				</p>

				<div className="mt-5 flex justify-center">
					<QRCodeSVG value={shareUrl} size={180} />
				</div>

				<div className="mt-4 flex items-center gap-2 rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
					<span className="flex-1 truncate text-sm text-gray-700">
						{shareUrl}
					</span>
					<button
						type="button"
						onClick={handleCopy}
						className="shrink-0 rounded bg-brand-600 px-3 py-1 text-sm font-medium text-white hover:bg-brand-700 transition-colors"
					>
						{t("achievements.shareCopyLink")}
					</button>
				</div>
			</div>
		</div>
	);
}
