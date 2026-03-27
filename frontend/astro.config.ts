import { defineConfig, envField } from 'astro/config';
import node from '@astrojs/node';
import react from '@astrojs/react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  output: 'server',
  adapter: node({ mode: 'standalone' }),
  integrations: [react()],
  vite: {
    plugins: [tailwindcss()],
  },
  env: {
    schema: {
      KEYCLOAK_AUTHORITY_URL: envField.string({ context: "server", access: "secret" }),
      REDIRECT_URI: envField.string({ context: "server", access: "secret" }),
      API_URL: envField.string({ context: "server", access: "secret" }),
    }
  }
});
