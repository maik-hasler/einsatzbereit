import js from "@eslint/js";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import prettier from "eslint-config-prettier";
import i18next from "eslint-plugin-i18next";
import jsxA11y from "eslint-plugin-jsx-a11y";

export default tseslint.config(
	js.configs.recommended,
	...tseslint.configs.strict,
	{
		plugins: { "react-hooks": reactHooks },
		rules: {
			"react-hooks/rules-of-hooks": "error",
			"react-hooks/exhaustive-deps": "warn",
		},
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { "jsx-a11y": jsxA11y },
		rules: jsxA11y.flatConfigs.recommended.rules,
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { i18next },
		rules: {
			// #1280: mode stays "jsx-text-only" - this rule's "jsx-only" mode is
			// the only way to make it look at JSX attribute values at all
			// (jsx-text-only structurally can't: a literal whose direct parent
			// is a JSXAttribute is never JSXElement/JSXFragment, so it's
			// filtered out before any jsx-attributes config is ever consulted,
			// regardless of what that config excludes - which is why the old
			// `ignoreAttribute` list here was dead code twice over, once for
			// using v5 syntax against the installed v6 plugin, and once
			// because the mode made it unreachable either way).
			//
			// Flipping to "jsx-only" was tried and reverted: that mode also
			// checks every OTHER string literal lexically nested anywhere
			// inside JSX - helper-function call arguments, style-object
			// properties, ternary branches - not just attributes and text.
			// That surfaced ~250 pre-existing hits across the app that are
			// technical identifiers and CSS values, not untranslated user
			// text, and fixing them is a dedicated remediation pass (working
			// through callees/jsx-components exclusions plus the genuine
			// finds), not something to fold into an unrelated i18n batch.
			"i18next/no-literal-string": [
				"error",
				{
					mode: "jsx-text-only",
				},
			],
		},
	},
	prettier,
	{
		ignores: ["dist/", "node_modules/", "scripts/", "public/"],
	},
);
