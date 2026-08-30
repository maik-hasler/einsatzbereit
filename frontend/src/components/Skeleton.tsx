import { skeletonClass } from "../lib/skeletonClasses";

interface Props {
	className?: string;
}

export default function Skeleton({ className = "" }: Props) {
	return <div aria-hidden="true" className={skeletonClass(className)} />;
}
