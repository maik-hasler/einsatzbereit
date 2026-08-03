import { labelClass } from "../lib/formClasses";

export default function Field({
	label,
	id,
	children,
}: {
	label: string;
	id?: string;
	children: React.ReactNode;
}) {
	return (
		<div>
			<label htmlFor={id} className={labelClass}>
				{label}
			</label>
			{children}
		</div>
	);
}
