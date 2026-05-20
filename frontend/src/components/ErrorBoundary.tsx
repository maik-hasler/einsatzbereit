import { Component, type ReactNode } from "react";
import i18next from "i18next";

interface Props {
	children: ReactNode;
}

interface State {
	hasError: boolean;
	error: Error | null;
}

export default class ErrorBoundary extends Component<Props, State> {
	constructor(props: Props) {
		super(props);
		this.state = { hasError: false, error: null };
	}

	static getDerivedStateFromError(error: Error): State {
		return { hasError: true, error };
	}

	componentDidCatch(error: Error, info: React.ErrorInfo) {
		console.error(
			"[ErrorBoundary] Uncaught error:",
			error,
			info.componentStack,
		);
	}

	handleBack = () => {
		this.setState({ hasError: false, error: null });
		window.history.back();
	};

	render() {
		if (this.state.hasError) {
			const t = i18next.t.bind(i18next);
			return (
				<div className="flex min-h-screen flex-col items-center justify-center gap-6 px-4 text-center">
					<h1 className="text-3xl font-bold text-gray-900">
						{t("error.boundaryTitle")}
					</h1>
					<p className="max-w-md text-gray-500">{t("error.boundaryMessage")}</p>
					<div className="flex gap-3">
						<button
							onClick={this.handleBack}
							className="rounded border border-gray-300 px-4 py-2 text-sm hover:bg-gray-50"
						>
							{t("error.goBack")}
						</button>
						<button
							onClick={() => window.location.reload()}
							className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
						>
							{t("error.reload")}
						</button>
					</div>
				</div>
			);
		}

		return this.props.children;
	}
}
