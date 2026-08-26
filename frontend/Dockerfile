FROM node:25.9.0-slim AS build
WORKDIR /app
RUN npm install -g pnpm
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .

# No defaults (#2207): the runtime values in docker-entrypoint.d/99-runtime-
# config.sh are what every deployment actually uses (see frontend/AGENTS.md's
# "Released image"); these build-time ARGs only seed the bundle's offline
# fallback (see src/lib/runtimeConfig.ts) for the rare cold start where the
# PWA cannot reach /config.js at all. A localhost default there silently
# pointed that fallback at the visitor's own machine instead of failing
# loudly, so an unset ARG is left to build empty rather than a value that
# looks plausible while being wrong for every deployment but one.
ARG VITE_KEYCLOAK_AUTHORITY_URL
ARG VITE_KEYCLOAK_CLIENT_ID
ARG VITE_API_URL

RUN VITE_KEYCLOAK_AUTHORITY_URL=$VITE_KEYCLOAK_AUTHORITY_URL \
	VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID \
	VITE_API_URL=$VITE_API_URL \
	pnpm run build

FROM nginx:1.31-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/nginx.conf.template
COPY docker-entrypoint.d/99-runtime-config.sh /docker-entrypoint.d/99-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/99-runtime-config.sh
EXPOSE 80
