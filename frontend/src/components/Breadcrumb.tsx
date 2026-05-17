import { Link } from "react-router";
import type { BreadcrumbItem } from "../contexts/ToolbarContext";

export default function Breadcrumb({ items }: { items: BreadcrumbItem[] }) {
	return (
		<nav
			aria-label="Breadcrumb"
			className="flex items-center gap-1 text-sm text-gray-500 min-w-0 overflow-hidden"
		>
			{items.map((item, index) => (
				<span key={index} className="flex items-center gap-1 min-w-0 shrink-0">
					{index > 0 && (
						<span className="text-gray-300 shrink-0" aria-hidden="true">
							›
						</span>
					)}
					{item.href !== undefined ? (
						<Link
							to={item.href}
							className="hover:text-gray-800 transition-colors truncate shrink"
						>
							{item.label}
						</Link>
					) : (
						<span className="font-medium text-gray-900 truncate shrink">
							{item.label}
						</span>
					)}
				</span>
			))}
		</nav>
	);
}
