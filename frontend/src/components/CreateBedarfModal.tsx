import { useState } from "react";
import type { CreateBedarfRequest } from "../client/api-client";

interface Props {
  organisationId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export default function CreateBedarfModal({ organisationId, onClose, onSuccess }: Props) {
  const [form, setForm] = useState<CreateBedarfRequest>({ title: "", description: "", organisationId });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await fetch("/api/bedarfe", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(form),
      });

      if (res.status === 401) {
        // Session expired — redirect to login
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
    // Backdrop
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
    >
      {/* Modal card — stop click propagation so backdrop click doesn't close from inside */}
      <div
        className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="mb-4 text-xl font-semibold">Bedarf erstellen</h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium">Titel</label>
            <input
              type="text"
              required
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Beschreibung</label>
            <textarea
              required
              rows={4}
              value={form.description}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
            />
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
