import js from "@eslint/js";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import prettier from "eslint-config-prettier";
import i18next from "eslint-plugin-i18next";
import jsxA11y from "eslint-plugin-jsx-a11y";
import tailwindcss from "eslint-plugin-tailwindcss";

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
		rules: {
			"no-restricted-syntax": [
				"error",
				{
					selector:
						":matches(Literal[value=/text-\\[/], TemplateElement[value.raw=/text-\\[/])",
					message:
						"Arbitrary Tailwind text size (text-[...]) bypasses the type scale defined in @theme - use a scale step (text-xs, text-sm, ...) instead, or add a named step to @theme if the scale genuinely needs one.",
				},
			],
		},
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { i18next },
		rules: {
			"i18next/no-literal-string": [
				"error",
				{
					mode: "jsx-text-only",
				},
			],
		},
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { tailwindcss },
		settings: {
			tailwindcss: {
				cssConfigPath: "./src/styles/global.css",
			},
		},
		rules: {
			"tailwindcss/classnames-order": "warn",
			"tailwindcss/no-contradicting-classname": "error",
			"tailwindcss/no-unnecessary-arbitrary-value": "warn",
			"tailwindcss/enforces-negative-arbitrary-values": "warn",
		},
	},
	{
		files: ["src/**/*.test.{ts,tsx}", "src/test/**/*.{ts,tsx}"],
		rules: {
			"i18next/no-literal-string": "off",
		},
	},
	prettier,
	{
		// coverage/, reports/ and .stryker-tmp/ are generated: `pnpm test:coverage`
		// and `pnpm mutation` write them, and .gitignore does not stop ESLint from
		// walking into them. Stryker's sandbox in particular is a full copy of the
		// project, so without this `pnpm lint` fails on its vendored coverage
		// scripts after any local mutation run.
		ignores: [
			"dist/",
			"node_modules/",
			"scripts/",
			"public/",
			"coverage/",
			"reports/",
			".stryker-tmp/",
		],
	},
);
