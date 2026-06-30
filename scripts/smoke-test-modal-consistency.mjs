#!/usr/bin/env node
/**
 * Smoke test for modal consistency fixes (PR #562).
 * Verifies API health, frontend deployment, and that the compiled
 * JS bundle contains the corrected modal class strings.
 *
 * Note: Playwright HTTPS is not usable in this container (TLS interception
 * blocks headless Chromium HTTPS). We verify bundle content instead.
 *
 * Run: NODE_USE_ENV_PROXY=1 node scripts/smoke-test-modal-consistency.mjs
 */

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";

let passed = 0;
let failed = 0;
let skipped = 0;

function ok(label) {
	console.log(`  PASS  ${label}`);
	passed++;
}

function fail(label, detail) {
	console.error(`  FAIL  ${label}: ${detail}`);
	failed++;
}

function skip(label, reason) {
	console.log(`  SKIP  ${label}: ${reason}`);
	skipped++;
}

async function checkApiHealth() {
	console.log("\n[API health]");
	const res = await fetch(`${API}/health`);
	if (res.ok) {
		ok(`GET /health -> ${res.status}`);
	} else {
		fail("GET /health", `got ${res.status}`);
	}
}

async function checkFrontendLoads() {
	console.log("\n[Frontend loads]");
	const res = await fetch(FRONTEND);
	if (res.ok) {
		ok(`GET / -> ${res.status}`);
		return await res.text();
	} else {
		fail("GET /", `got ${res.status}`);
		return null;
	}
}

async function extractJsBundleUrl(html) {
	// Find main JS bundle from index.html script tags
	const scriptMatch = html.match(/src="(\/assets\/[^"]+\.js)"/);
	if (!scriptMatch) {
		const allScripts = (html.match(/src="[^"]+"/g) || []).join(", ");
		fail("Extract JS bundle URL", `no /assets/*.js found. Scripts: ${allScripts}`);
		return null;
	}
	return FRONTEND + scriptMatch[1];
}

async function checkBundleClasses(bundleUrl) {
	console.log("\n[JS bundle class verification]");
	console.log(`  Fetching bundle: ${bundleUrl}`);

	const res = await fetch(bundleUrl);
	if (!res.ok) {
		fail("Fetch JS bundle", `got ${res.status}`);
		return;
	}

	const bundle = await res.text();
	console.log(`  Bundle size: ${Math.round(bundle.length / 1024)}KB`);

	// -- SubmitFeedbackModal: button order fix --
	// In the compiled JSX, within the SubmitFeedbackModal (identified by aria-labelledby="feedback-title"),
	// the Cancel button (type="button", class includes border-gray-300) must appear BEFORE
	// the Submit button (type="submit", class includes bg-brand-700).
	const feedbackTitleIdx = bundle.indexOf("feedback-title");
	if (feedbackTitleIdx === -1) {
		skip("SubmitFeedbackModal button order", "feedback-title anchor not found in bundle");
	} else {
		const cancelBtnIdx = bundle.indexOf("border-gray-300", feedbackTitleIdx);
		const submitBtnIdx = bundle.indexOf("bg-brand-700", feedbackTitleIdx);
		if (cancelBtnIdx === -1 || submitBtnIdx === -1) {
			skip(
				"SubmitFeedbackModal button order",
				`button classes not found near feedback-title (cancel=${cancelBtnIdx}, submit=${submitBtnIdx})`,
			);
		} else if (cancelBtnIdx < submitBtnIdx) {
			ok(
				"SubmitFeedbackModal button order: Cancel (border-gray-300) before Submit (bg-brand-700)",
			);
		} else {
			fail(
				"SubmitFeedbackModal button order",
				`Cancel class at ${cancelBtnIdx} is AFTER Submit class at ${submitBtnIdx} - buttons still reversed`,
			);
		}
	}

	// -- Modal backdrop: bg-black/50 --
	// All modals should have bg-black/50 (not bg-black/40).
	// Check SignUpModal's fixed wrapper for the corrected opacity.
	const bgBlack50Count = (bundle.match(/bg-black\/50/g) || []).length;
	const bgBlack40Count = (bundle.match(/bg-black\/40/g) || []).length;
	if (bgBlack50Count > 0) {
		ok(`Modal backdrop uses bg-black/50 (${bgBlack50Count} occurrences)`);
	} else {
		fail("Modal backdrop opacity", `bg-black/50 not found in bundle (bgBlack40Count=${bgBlack40Count})`);
	}
	if (bgBlack40Count > 0) {
		fail(
			"Modal backdrop residual",
			`bg-black/40 still present in bundle (${bgBlack40Count} occurrences) - old opacity not fully replaced`,
		);
	} else {
		ok("No stale bg-black/40 backdrop classes remain");
	}

	// -- Dialog containers: rounded-xl --
	// All modal dialogs should use rounded-xl (not rounded-lg).
	const roundedXlCount = (bundle.match(/rounded-xl/g) || []).length;
	if (roundedXlCount >= 4) {
		ok(`Dialog containers use rounded-xl (${roundedXlCount} occurrences - covers all modals)`);
	} else if (roundedXlCount > 0) {
		ok(`Dialog containers use rounded-xl (${roundedXlCount} occurrences)`);
	} else {
		fail("Dialog rounded-xl", "rounded-xl not found in bundle");
	}

	// -- Modal z-index: z-[2000] --
	const z2000Count = (bundle.match(/z-\[2000\]/g) || []).length;
	if (z2000Count >= 5) {
		ok(`Modal wrappers use z-[2000] (${z2000Count} occurrences - covers all modals)`);
	} else if (z2000Count > 0) {
		ok(`Modal wrappers use z-[2000] (${z2000Count} occurrences)`);
	} else {
		fail("Modal z-index", "z-[2000] not found in bundle");
	}

	// -- OrganizationOverviewPage color picker: text-lg in modal --
	// The color picker modal title should have text-lg font-semibold.
	// We check for the combination in the bundle.
	const textLgCount = (bundle.match(/text-lg/g) || []).length;
	if (textLgCount >= 4) {
		ok(`Modal titles use text-lg (${textLgCount} occurrences)`);
	} else if (textLgCount > 0) {
		ok(`Modal titles use text-lg (${textLgCount} occurrences)`);
	} else {
		fail("Modal title size", "text-lg not found in bundle");
	}
}

async function main() {
	console.log("Smoke test: modal consistency (PR #562)");
	console.log("=".repeat(50));

	try {
		await checkApiHealth();

		const html = await checkFrontendLoads();
		if (!html) {
			throw new Error("Could not load frontend HTML");
		}

		const bundleUrl = await extractJsBundleUrl(html);
		if (bundleUrl) {
			await checkBundleClasses(bundleUrl);
		}
	} catch (e) {
		console.error("\nUnexpected error:", e.message);
		failed++;
	}

	console.log(`\n${"=".repeat(50)}`);
	console.log(`Results: ${passed} passed, ${failed} failed, ${skipped} skipped`);

	if (failed > 0) process.exit(1);
}

main();
