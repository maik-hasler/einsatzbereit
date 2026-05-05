import { Link } from "react-router";
import PageEaten from "../assets/page-eaten.svg?react";

export default function NotFoundPage() {
	return (
		<div className="flex min-h-[60vh] flex-col items-center justify-center gap-8 text-center">
			<PageEaten className="w-72 text-brand-500 sm:w-80" />
			<div className="max-w-md">
				<h1 className="mb-3 text-3xl font-bold text-gray-900">
					Seite nicht gefunden
				</h1>
				<p className="mb-8 text-gray-600">
					Die aufgerufene Seite existiert nicht. Sie könnte verschoben, gelöscht
					oder nie vorhanden gewesen sein.
				</p>
				<Link
					to="/"
					className="inline-block rounded-md bg-brand-500 px-5 py-2.5 text-sm font-medium text-white hover:bg-brand-600"
				>
					Zur Startseite
				</Link>
			</div>
		</div>
	);
}
