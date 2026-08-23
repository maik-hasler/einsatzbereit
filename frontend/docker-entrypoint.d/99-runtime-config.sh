#!/bin/sh
set -eu

config="/usr/share/nginx/html/config.js"

if [ -f "$config" ]; then
	tmp="$(mktemp)"
	envsubst '${VITE_KEYCLOAK_AUTHORITY_URL} ${VITE_KEYCLOAK_CLIENT_ID} ${VITE_API_URL}' < "$config" > "$tmp"
	mv "$tmp" "$config"
	chmod 644 "$config"
fi

# The Content-Security-Policy's connect-src/frame-src need the backend API
# and Keycloak origins (scheme+host, no path), derived from the same env vars
# as config.js above rather than hardcoded, so whoever runs this image is not
# silently locked to somebody else's origins. img-src additionally needs the
# MinIO storage origin (STORAGE_PUBLIC_URL, matching
# the backend's Storage__PublicEndpoint) since uploaded org logos/opportunity
# banners/avatars are served from there, not from the API origin.
: "${VITE_API_URL:=http://localhost:5000}"
: "${VITE_KEYCLOAK_AUTHORITY_URL:=http://localhost:8080/realms/einsatzbereit}"
: "${STORAGE_PUBLIC_URL:=http://localhost:9000}"

url_origin() {
	proto="${1%%://*}"
	rest="${1#*://}"
	host="${rest%%/*}"
	printf '%s://%s' "$proto" "$host"
}

CSP_API_ORIGIN="$(url_origin "$VITE_API_URL")"
CSP_KEYCLOAK_ORIGIN="$(url_origin "$VITE_KEYCLOAK_AUTHORITY_URL")"
CSP_STORAGE_ORIGIN="$(url_origin "$STORAGE_PUBLIC_URL")"
export CSP_API_ORIGIN CSP_KEYCLOAK_ORIGIN CSP_STORAGE_ORIGIN

envsubst '${CSP_API_ORIGIN} ${CSP_KEYCLOAK_ORIGIN} ${CSP_STORAGE_ORIGIN}' \
	< /etc/nginx/nginx.conf.template \
	> /etc/nginx/conf.d/default.conf
