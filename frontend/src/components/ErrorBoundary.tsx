import { Component, type ReactNode } from "react";
import i18next from "i18next";
import { statusTitleClass } from "../lib/headingClasses";
import Button from "./Button";
import RouteState from "./RouteState";
import { isDynamicImportError } from "../lib/dynamicImportError";
import { getOnlineStatus, subscribeOnlineStatus } from "../lib/onlineStatus";

interface Props {
	children: ReactNode;
	/** Renders in place of the default full-page fallback - for a boundary
	 * scoped to a smaller region (e.g. a single dashboard widget) where the
	 * full-page "Something went wrong" UI would break the surrounding layout. */
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
		// #1955: every route is lazy-loaded (App.tsx), so navigating to one
		// whose chunk was never fetched throws a plain TypeError straight out of
		// the dynamic import() - not routed through useOnlineStatus/RouteState at
		// all, unlike every other offline-aware surface in the app.
		this.unsubscribeOnlineStatus = subscribeOnlineStatus(() => {
			const online = getOnlineStatus();
			const cameBackOnline = online && !this.state.online;
			// React.lazy() caches its import() promise - and a rejection - for
			// the lifetime of the page (App.tsx's lazy() calls are module-level
			// constants shared by every render), so clearing `hasError` here
			// would just re-render straight into the exact same cached rejection
			// and re-throw it instantly. Only a real reload re-fetches the chunk
			// from scratch, mirroring what a visitor who reloads by hand already
			// gets once back online - and matching what routeState.offline's
			// reused copy ("we load the page again - you do not have to do
			// anything") promises.
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
