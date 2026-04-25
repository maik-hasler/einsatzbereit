export function formatOccurrence(occurrence: string): string {
  return occurrence === "Recurring" ? "Regelmäßig" : "Einmalig";
}

export function formatParticipationType(type: string): string {
  return type === "Waitlist" ? "Warteliste" : "Einzelkontakt";
}

export function formatDateTime(dt: string): string {
  return new Date(dt).toLocaleString("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}
