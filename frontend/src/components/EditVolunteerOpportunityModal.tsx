import { useState } from "react";
import type { VolunteerOpportunityDetails } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

interface Props {
  opportunity: VolunteerOpportunityDetails;
  onClose: () => void;
  onSuccess: () => void;
}

export default function EditVolunteerOpportunityModal({ opportunity, onClose, onSuccess }: Props) {
  const api = useApiClient();

  const [title, setTitle] = useState(opportunity.title);
  const [description, setDescription] = useState(opportunity.description);
  const [isRemote, setIsRemote] = useState(opportunity.isRemote);
  const [street, setStreet] = useState(opportunity.street ?? "");
  const [houseNumber, setHouseNumber] = useState(opportunity.houseNumber ?? "");
  const [zipCode, setZipCode] = useState(opportunity.zipCode ?? "");
  const [city, setCity] = useState(opportunity.city ?? "");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await api.updateVolunteerOpportunity(opportunity.id, {
        title,
        description,
        isRemote,
        street: isRemote ? undefined : street,
        houseNumber: isRemote ? undefined : houseNumber,
        zipCode: isRemote ? undefined : zipCode,
        city: isRemote ? undefined : city,
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
      <div className="w-full max-w-lg rounded-lg bg-white p-6 shadow-xl overflow-y-auto max-h-screen">
        <h2 className="mb-4 text-lg font-semibold">Bedarf bearbeiten</h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Titel</label>
            <input
              value={title}
              onChange={e => setTitle(e.target.value)}
              required
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Beschreibung</label>
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              required
              rows={3}
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isRemote"
              checked={isRemote}
              onChange={e => setIsRemote(e.target.checked)}
              className="h-4 w-4"
            />
            <label htmlFor="isRemote" className="text-sm text-gray-700">Remote (kein Standort erforderlich)</label>
          </div>

          {!isRemote && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Straße</label>
                <input value={street} onChange={e => setStreet(e.target.value)} required className="w-full rounded border px-3 py-2 text-sm" />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Hausnummer</label>
                <input value={houseNumber} onChange={e => setHouseNumber(e.target.value)} required className="w-full rounded border px-3 py-2 text-sm" />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">PLZ</label>
                <input value={zipCode} onChange={e => setZipCode(e.target.value)} required maxLength={5} className="w-full rounded border px-3 py-2 text-sm" />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Stadt</label>
                <input value={city} onChange={e => setCity(e.target.value)} required className="w-full rounded border px-3 py-2 text-sm" />
              </div>
            </div>
          )}

          {error && <p className="text-sm text-red-600">{error}</p>}

          <div className="flex justify-end gap-2">
            <button type="button" onClick={onClose} className="rounded px-4 py-2 text-sm text-gray-600 hover:bg-gray-100">
              Abbrechen
            </button>
            <button type="submit" disabled={submitting} className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800 disabled:opacity-50">
              {submitting ? "Wird gespeichert…" : "Speichern"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
