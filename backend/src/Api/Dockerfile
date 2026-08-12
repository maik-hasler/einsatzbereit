FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS base
# GitHub Actions runners live on Azure, where the generic archive.ubuntu.com /
# security.ubuntu.com mirrors are known to be unreliable; route both pockets
# through the Azure-optimized mirror instead (same one the runner host itself uses).
RUN sed -i -E 's/(archive|security)\.ubuntu\.com/azure.archive.ubuntu.com/g' /etc/apt/sources.list.d/ubuntu.sources && \
	printf 'Acquire::Retries "3";\n' > /etc/apt/apt.conf.d/99-retry && \
	apt-get update && \
	apt-get install -y --no-install-recommends curl && \
	rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /repo
COPY . .
RUN dotnet restore "src/Api/Api.csproj"
WORKDIR "/repo/src/Api"
RUN dotnet build "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
