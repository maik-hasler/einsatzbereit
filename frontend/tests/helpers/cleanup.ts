import pg from 'pg';

const { Client } = pg;

const KEYCLOAK_ADMIN_TOKEN_URL = 'http://localhost:8080/realms/master/protocol/openid-connect/token';
const KEYCLOAK_ORGS_URL = 'http://localhost:8080/admin/realms/einsatzbereit/organizations';

async function truncateDatabase(): Promise<void> {
  const client = new Client({
    host: 'localhost',
    port: 5432,
    database: 'einsatzbereit',
    user: 'postgres',
    password: 'postgres',
  });
  await client.connect();
  await client.query('TRUNCATE TABLE volunteer_opportunity, organization CASCADE');
  await client.end();
}

async function deleteKeycloakOrganizations(): Promise<void> {
  const tokenRes = await fetch(KEYCLOAK_ADMIN_TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: 'admin',
      password: 'admin',
    }),
  });
  const { access_token } = await tokenRes.json() as { access_token: string };

  const orgsRes = await fetch(KEYCLOAK_ORGS_URL, {
    headers: { Authorization: `Bearer ${access_token}` },
  });
  const orgs = await orgsRes.json() as Array<{ id: string }>;

  await Promise.all(
    orgs.map((org) =>
      fetch(`${KEYCLOAK_ORGS_URL}/${org.id}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${access_token}` },
      })
    )
  );
}

export async function resetState(): Promise<void> {
  await Promise.all([truncateDatabase(), deleteKeycloakOrganizations()]);
}
