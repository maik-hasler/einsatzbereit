interface Props {
	className?: string;
}

export default function Skeleton({ className = "" }: Props) {
	return (
		<div
			aria-hidden="true"
			className={`animate-pulse rounded-md bg-gray-200 motion-reduce:animate-none ${className}`}
		/>
	);
}
