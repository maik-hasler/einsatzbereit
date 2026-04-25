FROM node:25.9.0-slim AS build
WORKDIR /app
RUN npm install -g pnpm
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .

ARG VITE_KEYCLOAK_AUTHORITY_URL=http://localhost:8080/realms/einsatzbereit
ARG VITE_KEYCLOAK_CLIENT_ID=frontend
ARG VITE_API_URL=http://localhost:5000

RUN VITE_KEYCLOAK_AUTHORITY_URL=$VITE_KEYCLOAK_AUTHORITY_URL \
    VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID \
    VITE_API_URL=$VITE_API_URL \
    pnpm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
