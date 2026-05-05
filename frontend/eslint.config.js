import js from "@eslint/js";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import prettier from "eslint-config-prettier";
import i18next from "eslint-plugin-i18next";

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
		plugins: { i18next },
		rules: {
			"i18next/no-literal-string": [
				"error",
				{
					ignoreAttribute: [
						"className",
						"id",
						"data-testid",
						"href",
						"type",
						"name",
						"strokeLinecap",
						"strokeLinejoin",
						"viewBox",
						"fill",
						"stroke",
						"d",
						"target",
						"rel",
						"pattern",
						"maxLength",
						"rows",
						"style",
						"placeholder",
					],
				},
			],
		},
	},
	prettier,
	{
		ignores: ["dist/", "node_modules/"],
	},
);
