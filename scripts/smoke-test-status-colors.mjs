// Smoke test: engagement status badge colors and brand styling (PR #561 / issues #536, #537)
// Verifies that the deployed CSS and JS contain the correct engagement status
// color classes from the shared ENGAGEMENT_STATUS_COLORS map (no regression to blue-*).
// Uses curl-equivalent fetch assertions (no browser required).
import https from "https";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

let passed = 0;
let failed = 0;

function ok(label) {
  console.log(`  PASS  ${label}`);
  passed++;
}
function fail(label, detail) {
  console.error(`  FAIL  ${label}: ${detail}`);
  failed++;
}

function fetch(url) {
  return new Promise((resolve, reject) => {
    const req = https.get(url, { rejectUnauthorized: false }, (res) => {
      let data = "";
      res.on("data", (chunk) => (data += chunk));
      res.on("end", () => resolve({ status: res.statusCode, body: data }));
    });
    req.on("error", reject);
    req.end();
  });
}

async function run() {
  // 1. API health check
  console.log("\n[API Health]");
  const health = await fetch(`${API}/health`);
  health.status === 200
    ? ok(`API health: ${health.body.trim()}`)
    : fail("API health", `HTTP ${health.status}`);

  // 2. Frontend HTML
  console.log("\n[Frontend HTML]");
  const html = await fetch(FRONTEND);
  html.status === 200
    ? ok("Frontend serves HTTP 200")
    : fail("Frontend HTML", `HTTP ${html.status}`);

  const jsMatch = html.body.match(/\/assets\/([^"]+\.js)/);
  const cssMatch = html.body.match(/\/assets\/([^"]+\.css)/);

  if (!jsMatch || !cssMatch) {
    fail("Asset extraction", "Could not find JS/CSS asset paths in HTML");
    return;
  }
  const jsUrl = `${FRONTEND}${jsMatch[0]}`;
  const cssUrl = `${FRONTEND}${cssMatch[0]}`;
  ok(`Found assets: ${jsMatch[0]}, ${cssMatch[0]}`);

  // 3. CSS contains engagement status color classes
  console.log("\n[CSS engagement status colors]");
  const css = await fetch(cssUrl);
  const statusCssClasses = [
    "bg-yellow-50",
    "text-yellow-700",
    "bg-green-50",
    "text-green-700",
    "bg-red-50",
    "text-red-700",
    "bg-gray-100",
    "text-gray-500",
  ];
  for (const cls of statusCssClasses) {
    css.body.includes(cls)
      ? ok(`CSS contains .${cls}`)
      : fail(`CSS missing .${cls}`, "engagement status color not in CSS");
  }

  // 4. CSS contains brand color classes (PIN box brand styling)
  console.log("\n[CSS brand classes]");
  const brandClasses = [
    "bg-brand-50",
    "border-brand-200",
    "text-brand-900",
    "text-brand-800",
    "text-brand-600",
  ];
  for (const cls of brandClasses) {
    css.body.includes(cls)
      ? ok(`CSS contains .${cls}`)
      : fail(`CSS missing .${cls}`, "brand class not in CSS");
  }

  // 5. JS bundle contains ENGAGEMENT_STATUS_COLORS strings
  console.log("\n[JS engagement status color strings]");
  const js = await fetch(jsUrl);
  const statusStrings = [
    "bg-yellow-50 text-yellow-700 border-yellow-100",
    "bg-green-50 text-green-700 border-green-100",
    "bg-red-50 text-red-700 border-red-100",
    "bg-gray-100 text-gray-500 border-gray-200",
  ];
  for (const str of statusStrings) {
    js.body.includes(str)
      ? ok(`JS bundle contains "${str}"`)
      : fail(`JS bundle missing "${str}"`, "status color map not in JS");
  }

  // 6. No off-brand blue-* classes in CSS
  console.log("\n[Off-brand color check]");
  const blueClasses = [
    "bg-blue-50",
    "border-blue-200",
    "text-blue-900",
    "text-blue-800",
    "text-blue-600",
  ];
  const foundBlue = blueClasses.filter((c) => css.body.includes(c));
  foundBlue.length === 0
    ? ok("No off-brand blue-* classes in CSS")
    : fail(
        "Off-brand blue-* classes found",
        `found: ${foundBlue.join(", ")} — potential regression`,
      );

  // Summary
  console.log(`\n${"=".repeat(60)}`);
  console.log(`Results: ${passed} passed, ${failed} failed`);
  if (failed > 0) {
    console.error("SMOKE TEST FAILED");
    process.exit(1);
  } else {
    console.log("SMOKE TEST PASSED");
  }
}

run().catch((err) => {
  console.error("Smoke test error:", err);
  process.exit(1);
});
