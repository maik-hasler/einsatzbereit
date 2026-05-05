import { Link } from "react-router";
import PageEaten from "../assets/page-eaten.svg?react";

export default function NotFoundPage() {
	return (
		<div className="relative flex min-h-[70vh] items-center justify-center overflow-hidden px-4 text-center">
			<PageEaten className="absolute top-1/2 text-brand-500 w-85 translate-y-[-60%] opacity-35 sm:w-105" />

			{/* Content */}
			<div className="relative z-10 max-w-md">
				<h1 className="mb-4 text-4xl font-bold tracking-tight text-brand-700">
					Seite nicht gefunden
				</h1>

				<p className="mb-8 text-black">
					Die Seite existiert nicht oder wurde verschoben. Vielleicht hat der
					Hund sie gefressen...
				</p>

				<Link
					to="/"
					className="inline-flex items-center gap-2 rounded-full bg-brand-600 px-6 py-3 text-sm font-medium text-white shadow-lg hover:bg-brand-700"
				>
					Zur Startseite
				</Link>
			</div>
		</div>
	);
}
