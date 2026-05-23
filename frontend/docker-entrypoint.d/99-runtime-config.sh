#!/bin/sh
# Substitutes runtime env vars into config.js at container start, so a single
# image can be deployed to any environment (build once, deploy anywhere).
set -eu

config="/usr/share/nginx/html/config.js"

if [ -f "$config" ]; then
	tmp="$(mktemp)"
	envsubst '${VITE_KEYCLOAK_AUTHORITY_URL} ${VITE_KEYCLOAK_CLIENT_ID} ${VITE_API_URL}' < "$config" > "$tmp"
	mv "$tmp" "$config"
	# mktemp creates files mode 600; nginx runs as a non-root user and would
	# return 403 without world-readable perms.
	chmod 644 "$config"
fi
