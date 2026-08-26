#!/bin/sh
set -eu

# Fail fast, before rendering anything, rather than silently falling back to
# a value that points at the visitor's own machine (#2207) - a missing or
# misspelled env var used to render as an empty substitution in config.js,
# which every API call and the Keycloak login would then silently resolve
# against http://localhost:... in the visitor's own browser.
: "${VITE_API_URL:?VITE_API_URL is required}"
: "${VITE_KEYCLOAK_AUTHORITY_URL:?VITE_KEYCLOAK_AUTHORITY_URL is required}"
: "${VITE_KEYCLOAK_CLIENT_ID:?VITE_KEYCLOAK_CLIENT_ID is required}"

config="/usr/share/nginx/html/config.js"

if [ -f "$config" ]; then
	tmp="$(mktemp)"
	envsubst '${VITE_KEYCLOAK_AUTHORITY_URL} ${VITE_KEYCLOAK_CLIENT_ID} ${VITE_API_URL} ${VITE_APP_VERSION}' < "$config" > "$tmp"
	mv "$tmp" "$config"
	chmod 644 "$config"
fi

# The Content-Security-Policy's connect-src/frame-src need the backend API
# and Keycloak origins (scheme+host, no path), derived from the same env vars
# as config.js above rather than hardcoded, so whoever runs this image is not
# silently locked to somebody else's origins. img-src additionally needs the
# MinIO storage origin (STORAGE_PUBLIC_URL, matching
# the backend's Storage__PublicEndpoint) since uploaded org logos/opportunity
# banners/avatars are served from there, not from the API origin. Unlike the
# three required above, STORAGE_PUBLIC_URL keeps a fallback: a missing value
# only narrows the CSP's img-src (broken images, loudly visible), not an
# origin that silently swallows every API call and login.
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
