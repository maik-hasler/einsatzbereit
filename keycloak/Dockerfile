FROM quay.io/keycloak/keycloak:26.7.2 AS builder

ENV KC_DB=postgres
ENV KC_HEALTH_ENABLED=true
ENV KC_METRICS_ENABLED=false

RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:26.7.2

COPY --from=builder /opt/keycloak/ /opt/keycloak/
COPY themes /opt/keycloak/themes
COPY realms /opt/keycloak/data/import

# --import-realm's default behavior skips a realm that already exists in the
# database, so restarts are idempotent by default. That default is overridden
# at runtime for staging/production by docker-compose.yml's
# KC_SPI_REALM_IMPORTER_FILE_IMPORT_BEHAVIOR=OVERWRITE_EXISTING, which makes
# every restart re-import and overwrite the realm from this image's baked
# copy instead - see keycloak/AGENTS.md for the resolved semantics and why.
ENTRYPOINT ["/opt/keycloak/bin/kc.sh", "start", "--optimized", "--import-realm"]
