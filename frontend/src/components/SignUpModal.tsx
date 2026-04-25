import { useState } from "react";
import type { TimeSlotDetail } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { formatDateTime } from "../lib/format";

interface Props {
  opportunityId: string;
  participationType: string;
  timeSlots: TimeSlotDetail[];
  onClose: () => void;
  onSuccess: () => void;
}

export default function SignUpModal({ opportunityId, participationType, timeSlots, onClose, onSuccess }: Props) {
  const api = useApiClient();
  const [selectedTimeSlotId, setSelectedTimeSlotId] = useState<string>("");
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isWaitlist = participationType === "Waitlist";

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await api.createEngagement(opportunityId, {
        type: isWaitlist ? "Waitlist" : "IndividualContact",
        timeSlotId: isWaitlist && selectedTimeSlotId ? selectedTimeSlotId : undefined,
        message: !isWaitlist ? message : undefined,
      });
      onSuccess();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unbekannter Fehler");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold">
          {isWaitlist ? "Auf Warteliste eintragen" : "Interesse bekunden"}
        </h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          {isWaitlist && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Zeitslot wählen</label>
              {timeSlots.length === 0 ? (
                <p className="text-sm text-gray-500">Keine Zeitslots verfügbar.</p>
              ) : (
                <select
                  value={selectedTimeSlotId}
                  onChange={e => setSelectedTimeSlotId(e.target.value)}
                  required
                  className="w-full rounded border px-3 py-2 text-sm"
                >
                  <option value="">Bitte wählen…</option>
                  {timeSlots.map(ts => (
                    <option key={ts.id} value={ts.id}>
                      {formatDateTime(ts.startDateTime as unknown as string)} - {formatDateTime(ts.endDateTime as unknown as string)} (max. {ts.maxParticipants})
                    </option>
                  ))}
                </select>
              )}
            </div>
          )}

          {!isWaitlist && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Nachricht</label>
              <textarea
                value={message}
                onChange={e => setMessage(e.target.value)}
                required
                rows={4}
                placeholder="Beschreibe kurz, warum du dich engagieren möchtest…"
                className="w-full rounded border px-3 py-2 text-sm"
              />
            </div>
          )}

          {error && <p className="text-sm text-red-600">{error}</p>}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded px-4 py-2 text-sm text-gray-600 hover:bg-gray-100"
            >
              Abbrechen
            </button>
            <button
              type="submit"
              disabled={submitting || (isWaitlist && timeSlots.length === 0)}
              className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800 disabled:opacity-50"
            >
              {submitting ? "Wird gesendet…" : "Anmelden"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
