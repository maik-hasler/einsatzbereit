#!/usr/bin/env bash
set -euo pipefail

# A previous session may have installed the SDK into $HOME/.dotnet without it
# being on this shell's PATH yet, so widen PATH before deciding to re-download.
export PATH="$HOME/.dotnet:$PATH"

if ! command -v dotnet &>/dev/null; then
	DOTNET_SDK_VERSION=$(grep -o '"version": *"[^"]*"' backend/global.json | head -1 | sed -E 's/.*"([0-9.]+)".*/\1/')
	echo "[SessionStart] dotnet not found - installing .NET SDK $DOTNET_SDK_VERSION..."
	curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
	chmod +x /tmp/dotnet-install.sh
	/tmp/dotnet-install.sh --version "$DOTNET_SDK_VERSION" --install-dir "$HOME/.dotnet"
	grep -qF '$HOME/.dotnet:$PATH' "$HOME/.bashrc" 2>/dev/null ||
		echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
fi

# IntegrationTests, not Api: the OpenAPI document regenerates from Api's own
# GenerateOpenApiDocuments target, but ApiClient.cs regenerates from the NSwag
# target that now lives in IntegrationTests.csproj. Building Api alone would
# refresh one committed artifact and leave the other stale, and the line above
# would still read as though it had done its job. IntegrationTests references
# Api, so this refreshes both.
echo "[SessionStart] Building backend (OpenAPI document + API client regeneration)..."
dotnet build backend/tests/IntegrationTests/IntegrationTests.csproj --configuration Debug --verbosity quiet

echo "[SessionStart] Formatting frontend..."
cd frontend && pnpm format:write
