const API_URL = import.meta.env.API_URL ?? 'http://localhost:5000';

export interface BedarfSummary {
  id: string;
  titel: string;
  ort: string;
  organisation: string;
  startzeitUtc: string;
  endzeitUtc: string | null;
}

export interface Bedarf extends BedarfSummary {
  beschreibung: string;
  erstelltAmUtc: string;
}

export async function fetchBedarfe(): Promise<BedarfSummary[]> {
  const response = await fetch(`${API_URL}/api/bedarfe`);
  if (!response.ok) {
    throw new Error(`Bedarfe konnten nicht geladen werden: ${response.status}`);
  }
  return response.json();
}

export async function fetchBedarf(id: string): Promise<Bedarf | null> {
  const response = await fetch(`${API_URL}/api/bedarfe/${id}`);
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Bedarf konnte nicht geladen werden: ${response.status}`);
  }
  return response.json();
}
