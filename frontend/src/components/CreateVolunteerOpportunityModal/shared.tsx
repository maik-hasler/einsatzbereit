import type { UseFormRegisterReturn } from "react-hook-form";
import { useTranslation } from "react-i18next";

function RequiredMark() {
	return (
		<span className="ml-0.5 text-red-400" aria-hidden="true">
			*
		</span>
	);
}

export function Stepper({
	current,
	errorSteps,
	onStepClick,
	steps,
	stepLabel,
}: {
	current: number;
	errorSteps: Set<number>;
	onStepClick: (n: number) => void;
	steps: string[];
	stepLabel: (n: number, label: string) => string;
}) {
	return (
		<ol className="mt-4 flex items-stretch gap-1.5">
			{steps.map((label, i) => {
				const n = i + 1;
				const isActive = n === current;
				const hasError = errorSteps.has(n);
				const isDone = n < current;
				return (
					<li key={label} className="min-w-0 flex-1">
						<button
							type="button"
							onClick={() => onStepClick(n)}
							aria-current={isActive ? "step" : undefined}
							aria-label={stepLabel(n, label)}
							data-testid={`wizard-stepper-${n}`}
							className="group flex w-full flex-col gap-1.5 rounded-md px-0.5 pb-1 text-left"
						>
							<span
								aria-hidden="true"
								className={`h-1 w-full rounded-full transition-colors ${
									hasError
										? "bg-red-400"
										: isActive || isDone
											? "bg-brand-600"
											: "bg-gray-200 group-hover:bg-brand-200"
								}`}
							/>
							<span
								className={`truncate text-xs font-semibold ${
									hasError
										? "text-red-600"
										: isActive
											? "text-brand-700"
											: isDone
												? "text-gray-700"
												: "text-gray-500 group-hover:text-gray-600"
								}`}
							>
								{label}
							</span>
						</button>
					</li>
				);
			})}
		</ol>
	);
}

/** Text field with a floating label, wired to a react-hook-form registration. */
export function FloatingField({
	id,
	label,
	registration,
	required = false,
	error,
	maxLength,
	multiline = false,
	rows = 5,
	showCount = false,
	displayValue,
	wrapperClassName,
	inputMode,
	pattern,
}: {
	id: string;
	label: string;
	registration: UseFormRegisterReturn;
	required?: boolean;
	error?: string;
	maxLength?: number;
	multiline?: boolean;
	rows?: number;
	showCount?: boolean;
	/** Live value for the character counter (from `watch`) - registration alone doesn't expose it. */
	displayValue?: string;
	wrapperClassName?: string;
	inputMode?: "numeric";
	pattern?: string;
}) {
	const fieldClass = `peer w-full rounded-xl border bg-white px-4 pb-2 pt-5 text-sm text-gray-900 shadow-sm transition ${
		error
			? "border-red-300 focus:border-red-400"
			: "border-gray-200 focus:border-brand-400"
	}`;
	const labelClass = `pointer-events-none absolute left-4 top-1.5 text-xs font-medium transition-all peer-placeholder-shown:top-3.5 peer-placeholder-shown:text-sm peer-placeholder-shown:font-normal peer-placeholder-shown:text-gray-600 peer-focus:top-1.5 peer-focus:text-xs peer-focus:font-medium ${
		error
			? "text-red-600 peer-focus:text-red-600"
			: "text-gray-600 peer-focus:text-brand-700"
	}`;
	const errorId = error ? `${id}-error` : undefined;

	return (
		<div className={wrapperClassName}>
			<div className="relative">
				{multiline ? (
					<textarea
						id={id}
						rows={rows}
						maxLength={maxLength}
						placeholder=" "
						aria-invalid={error ? true : undefined}
						aria-describedby={errorId}
						aria-required={required || undefined}
						className={fieldClass}
						{...registration}
					/>
				) : (
					<input
						id={id}
						type="text"
						maxLength={maxLength}
						placeholder=" "
						inputMode={inputMode}
						pattern={pattern}
						aria-invalid={error ? true : undefined}
						aria-describedby={errorId}
						aria-required={required || undefined}
						className={fieldClass}
						{...registration}
					/>
				)}
				<label htmlFor={id} className={labelClass}>
					{label}
					{required && <RequiredMark />}
				</label>
			</div>
			{error ? (
				<p id={errorId} className="mt-1 text-xs text-red-600" role="alert">
					{error}
				</p>
			) : (
				showCount &&
				maxLength !== undefined && (
					<CharCount current={displayValue?.length ?? 0} max={maxLength} />
				)
			)}
		</div>
	);
}

function CharCount({ current, max }: { current: number; max: number }) {
	const { t } = useTranslation();
	return (
		<p className="mt-1 text-right text-xs text-gray-500">
			{t("createOpportunity.charCount", { current, max })}
		</p>
	);
}
