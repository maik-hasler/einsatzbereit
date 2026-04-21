import pg from 'pg';

const { Client } = pg;

function createClient(): InstanceType<typeof Client> {
  return new Client({
    host: 'localhost',
    port: 5432,
    database: 'einsatzbereit',
    user: 'postgres',
    password: 'postgres',
  });
}

export async function seedTimeSlot(
  opportunityId: string,
  startDateTime: string,
  endDateTime: string,
  maxParticipants: number,
): Promise<string> {
  const client = createClient();
  await client.connect();
  const id = crypto.randomUUID();
  await client.query(
    `INSERT INTO time_slot (id, start_date_time, end_date_time, max_participants, volunteer_opportunity_id)
     VALUES ($1, $2, $3, $4, $5)`,
    [id, startDateTime, endDateTime, maxParticipants, opportunityId],
  );
  await client.end();
  return id;
}
