import { getLabelClass } from "../lib/formClasses";
import { RequiredMark } from "./RequiredMark";
import FieldError from "./FieldError";

export default function Field({
	label,
	id,
	required = false,
	error,
	children,
}: {
	label: string;
	id?: string;

	required?: boolean;
	error?: string;
	children: React.ReactNode;
}) {
	const errorId = id && error ? `${id}-error` : undefined;
	return (
		<div>
			<label htmlFor={id} className={getLabelClass(Boolean(error))}>
				{label}
				{required && <RequiredMark />}
			</label>
			{children}
			<FieldError id={errorId} message={error} />
		</div>
	);
}
