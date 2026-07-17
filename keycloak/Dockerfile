FROM quay.io/keycloak/keycloak:26.7.0 AS builder

ENV KC_DB=postgres
ENV KC_HEALTH_ENABLED=true
ENV KC_METRICS_ENABLED=false

RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:26.7.0

COPY --from=builder /opt/keycloak/ /opt/keycloak/
COPY themes /opt/keycloak/themes
COPY realms /opt/keycloak/data/import

# --import-realm imports the baked realm on first start; it is skipped once the
# realm already exists in the database, so restarts are idempotent.
ENTRYPOINT ["/opt/keycloak/bin/kc.sh", "start", "--optimized", "--import-realm"]
