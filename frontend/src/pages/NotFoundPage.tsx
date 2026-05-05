import { Link } from "react-router";

export default function NotFoundPage() {
	return (
		<>
			<h1 className="mb-4 text-4xl font-bold text-gray-900">
				404 – Seite nicht gefunden
			</h1>
			<p className="mb-8 text-lg text-gray-600">
				Die aufgerufene Seite existiert nicht. Sie könnte verschoben, gelöscht
				oder nie vorhanden gewesen sein.
			</p>
			<Link
				to="/"
				className="inline-block rounded-md bg-brand-500 px-5 py-2.5 text-sm font-medium text-white hover:bg-brand-600"
			>
				Zur Startseite
			</Link>
		</>
	);
}
