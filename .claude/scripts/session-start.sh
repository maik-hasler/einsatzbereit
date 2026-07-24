#!/usr/bin/env bash
set -euo pipefail

# Install .NET 10 SDK if dotnet is not on PATH
if ! command -v dotnet &>/dev/null; then
	DOTNET_SDK_VERSION=$(grep -o '"version": *"[^"]*"' backend/global.json | head -1 | sed -E 's/.*"([0-9.]+)".*/\1/')
	echo "[SessionStart] dotnet not found - installing .NET SDK $DOTNET_SDK_VERSION..."
	curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
	chmod +x /tmp/dotnet-install.sh
	/tmp/dotnet-install.sh --version "$DOTNET_SDK_VERSION" --install-dir "$HOME/.dotnet"
	export PATH="$HOME/.dotnet:$PATH"
	echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
fi

# Regenerate openapi-v1.json + api-client.ts + ApiClient.cs via NSwag post-build
echo "[SessionStart] Building backend (NSwag regeneration)..."
dotnet build backend/src/Api/Api.csproj --configuration Debug --verbosity quiet

# Apply Prettier formatting so no violations are committed
echo "[SessionStart] Formatting frontend..."
cd frontend && pnpm format:write
