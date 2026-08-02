import { Component, type ReactNode } from "react";
import i18next from "i18next";
import { statusTitleClass } from "../lib/headingClasses";
import Button from "./Button";

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
					<h1 className={`text-brand-700 ${statusTitleClass}`}>
						{t("error.boundaryTitle")}
					</h1>
					<p className="max-w-md text-gray-500">{t("error.boundaryMessage")}</p>
					<div className="flex gap-3">
						<Button variant="secondary" onClick={this.handleBack}>
							{t("error.goBack")}
						</Button>
						<Button onClick={() => window.location.reload()}>
							{t("error.reload")}
						</Button>
					</div>
				</div>
			);
		}

		return this.props.children;
	}
}
