import Skeleton from "./Skeleton";

/**
 * The loading stand-in for `OpportunityCard`, and the only one - the browse
 * grid used to draw a 128px image block over three short bars, an anatomy no
 * card has ever had (a card leads with a badge row, and the media band only
 * exists where `withMedia` is set), while the landing strip drew a third
 * shape of its own (#2329 F6). Same shell, same blocks in the same order, so
 * the grid does not re-lay itself out when the real cards arrive.
 */
export default function OpportunityCardSkeleton({
	withMedia = false,
}: {
	withMedia?: boolean;
}) {
	return (
		<div
			aria-hidden="true"
			className="flex flex-col overflow-hidden rounded-card border border-gray-100 bg-white shadow-resting"
		>
			{withMedia && <Skeleton className="h-32 w-full shrink-0 rounded-none" />}
			<div className="flex flex-1 flex-col p-4 sm:p-5">
				<div className="mb-2 flex items-center gap-1.5">
					<Skeleton className="h-5 w-20 rounded-full" />
					<Skeleton className="h-5 w-16 rounded-full" />
					<Skeleton className="ml-auto h-5 w-24 rounded-full" />
				</div>
				<Skeleton className="h-6 w-3/4" />
				<Skeleton className="mt-1.5 h-5 w-1/2" />
				<Skeleton className="mt-2 h-4 w-full" />
				<Skeleton className="mt-1.5 h-4 w-2/3" />
				<div className="mt-4 flex items-center gap-3 border-t border-gray-100 pt-3">
					<Skeleton className="h-7 w-7 rounded-full" />
					<Skeleton className="h-4 w-28" />
					<Skeleton className="ml-auto h-3 w-16" />
				</div>
			</div>
		</div>
	);
}
