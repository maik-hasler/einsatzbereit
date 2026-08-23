import { Component, type ReactNode } from "react";
import i18next from "i18next";
import { statusTitleClass } from "../lib/headingClasses";
import Button from "./Button";
import RouteState from "./RouteState";
import { isDynamicImportError } from "../lib/dynamicImportError";
import { getOnlineStatus, subscribeOnlineStatus } from "../lib/onlineStatus";

interface Props {
	children: ReactNode;

	fallback?: ReactNode;
}

interface State {
	hasError: boolean;
	error: Error | null;
	online: boolean;
}

export default class ErrorBoundary extends Component<Props, State> {
	private unsubscribeOnlineStatus: (() => void) | null = null;

	constructor(props: Props) {
		super(props);
		this.state = { hasError: false, error: null, online: getOnlineStatus() };
	}

	static getDerivedStateFromError(
		error: Error,
	): Pick<State, "hasError" | "error"> {
		return { hasError: true, error };
	}

	componentDidMount() {
		this.unsubscribeOnlineStatus = subscribeOnlineStatus(() => {
			const online = getOnlineStatus();
			const cameBackOnline = online && !this.state.online;

			if (
				cameBackOnline &&
				this.state.hasError &&
				isDynamicImportError(this.state.error)
			) {
				window.location.reload();
				return;
			}
			this.setState({ online });
		});
	}

	componentWillUnmount() {
		this.unsubscribeOnlineStatus?.();
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
			if (this.props.fallback) return this.props.fallback;
			const t = i18next.t.bind(i18next);

			if (!this.state.online && isDynamicImportError(this.state.error)) {
				return (
					<RouteState
						variant="offline"
						title={t("routeState.offline.title")}
						message={t("routeState.offline.message")}

						onRetry={() => window.location.reload()}
					/>
				);
			}

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
