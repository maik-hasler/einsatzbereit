import { labelClass } from "../lib/formClasses";
import { RequiredMark } from "./RequiredMark";

export default function Field({
	label,
	id,
	required = false,
	children,
}: {
	label: string;
	id?: string;

	required?: boolean;
	children: React.ReactNode;
}) {
	return (
		<div>
			<label htmlFor={id} className={labelClass}>
				{label}
				{required && <RequiredMark />}
			</label>
			{children}
		</div>
	);
}
