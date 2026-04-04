import { useState } from "react";
import type { CreateVolunteerOpportunityRequest } from "../client/api-client";

interface Props {
  organizationId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export default function CreateVolunteerOpportunityModal({ organizationId, onClose, onSuccess }: Props) {
  const [form, setForm] = useState<CreateVolunteerOpportunityRequest>({
    title: "",
    description: "",
    organizationId,
    street: "",
    houseNumber: "",
    zipCode: "",
    city: "",
    occurrence: "OneTime",
    participationType: "Waitlist",
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await fetch("/api/volunteer-opportunities", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(form),
      });

      if (res.status === 401) {
        window.location.href = "/api/login";
        return;
      }
      if (!res.ok) throw new Error(`Fehler ${res.status}`);

      onSuccess();
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Unbekannter Fehler");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg rounded-lg bg-white p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="mb-4 text-xl font-semibold">Bedarf erstellen</h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="opportunity-title" className="mb-1 block text-sm font-medium">Titel</label>
            <input
              id="opportunity-title"
              type="text"
              required
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
            />
          </div>

          <div>
            <label htmlFor="opportunity-description" className="mb-1 block text-sm font-medium">Beschreibung</label>
            <textarea
              id="opportunity-description"
              required
              rows={3}
              value={form.description}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
            />
          </div>

          <fieldset className="space-y-3 rounded border p-3">
            <legend className="px-1 text-sm font-medium">Adresse</legend>
            <div className="flex gap-3">
              <div className="flex-1">
                <label className="mb-1 block text-sm text-gray-600">Straße</label>
                <input
                  type="text"
                  required
                  placeholder="Musterstraße"
                  value={form.street}
                  onChange={(e) => setForm((f) => ({ ...f, street: e.target.value }))}
                  className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
                />
              </div>
              <div className="w-24">
                <label className="mb-1 block text-sm text-gray-600">Nr.</label>
                <input
                  type="text"
                  required
                  placeholder="1a"
                  value={form.houseNumber}
                  onChange={(e) => setForm((f) => ({ ...f, houseNumber: e.target.value }))}
                  className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
                />
              </div>
            </div>
            <div className="flex gap-3">
              <div className="w-28">
                <label className="mb-1 block text-sm text-gray-600">PLZ</label>
                <input
                  type="text"
                  required
                  pattern="\d{5}"
                  maxLength={5}
                  placeholder="12345"
                  value={form.zipCode}
                  onChange={(e) => setForm((f) => ({ ...f, zipCode: e.target.value }))}
                  className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
                />
              </div>
              <div className="flex-1">
                <label className="mb-1 block text-sm text-gray-600">Ort</label>
                <input
                  type="text"
                  required
                  placeholder="Berlin"
                  value={form.city}
                  onChange={(e) => setForm((f) => ({ ...f, city: e.target.value }))}
                  className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
                />
              </div>
            </div>
          </fieldset>

          <div>
            <label className="mb-2 block text-sm font-medium">Häufigkeit</label>
            <div className="flex gap-4">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="occurrence"
                  value="OneTime"
                  checked={form.occurrence === "OneTime"}
                  onChange={(e) => setForm((f) => ({ ...f, occurrence: e.target.value }))}
                  className="accent-black"
                />
                Einmalig
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="occurrence"
                  value="Recurring"
                  checked={form.occurrence === "Recurring"}
                  onChange={(e) => setForm((f) => ({ ...f, occurrence: e.target.value }))}
                  className="accent-black"
                />
                Regelmäßig
              </label>
            </div>
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium">Teilnahmeart</label>
            <div className="flex gap-4">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="participationType"
                  value="Waitlist"
                  checked={form.participationType === "Waitlist"}
                  onChange={(e) => setForm((f) => ({ ...f, participationType: e.target.value }))}
                  className="accent-black"
                />
                Warteliste
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="participationType"
                  value="IndividualContact"
                  checked={form.participationType === "IndividualContact"}
                  onChange={(e) => setForm((f) => ({ ...f, participationType: e.target.value }))}
                  className="accent-black"
                />
                Einzelkontakt
              </label>
            </div>
          </div>

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
              disabled={loading}
              className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800 disabled:opacity-50"
            >
              {loading ? "Wird erstellt…" : "Erstellen"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
