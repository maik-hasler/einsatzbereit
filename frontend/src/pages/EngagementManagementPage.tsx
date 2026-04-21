import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import type { EngagementSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

const STATUS_LABELS: Record<string, string> = {
  Pending: "Ausstehend",
  Confirmed: "Bestätigt",
  Cancelled: "Abgesagt",
  Withdrawn: "Zurückgezogen",
};

const STATUS_COLORS: Record<string, string> = {
  Pending: "bg-yellow-50 text-yellow-700",
  Confirmed: "bg-green-50 text-green-700",
  Cancelled: "bg-red-50 text-red-700",
  Withdrawn: "bg-gray-100 text-gray-500",
};

export default function EngagementManagementPage() {
  const { opportunityId } = useParams<{ opportunityId: string }>();
  const navigate = useNavigate();
  const api = useApiClient();

  const [engagements, setEngagements] = useState<EngagementSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [processing, setProcessing] = useState<string | null>(null);

  useEffect(() => {
    if (!opportunityId) return;
    api.getEngagements(opportunityId)
      .then(setEngagements)
      .catch(err => setError(err.message))
      .finally(() => setLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opportunityId]);

  async function handleConfirm(engagementId: string) {
    setProcessing(engagementId + "-confirm");
    try {
      const updated = await api.confirmEngagement(engagementId);
      setEngagements(prev =>
        prev.map(e => e.id === engagementId ? { ...e, status: updated.status } : e)
      );
    } catch (err) {
      alert(err instanceof Error ? err.message : "Fehler beim Bestätigen");
    } finally {
      setProcessing(null);
    }
  }

  async function handleCancel(engagementId: string) {
    setProcessing(engagementId + "-cancel");
    try {
      const updated = await api.cancelEngagement(engagementId);
      setEngagements(prev =>
        prev.map(e => e.id === engagementId ? { ...e, status: updated.status } : e)
      );
    } catch (err) {
      alert(err instanceof Error ? err.message : "Fehler beim Absagen");
    } finally {
      setProcessing(null);
    }
  }

  return (
    <>
      <button
        onClick={() => navigate(-1)}
        className="mb-4 text-sm text-gray-500 hover:text-gray-800"
      >
        ← Zurück
      </button>

      <h1 className="mb-6 text-2xl font-bold text-gray-900">Bewerbungen verwalten</h1>

      {loading && <p className="text-gray-500">Wird geladen…</p>}
      {error && <p className="text-red-600">Fehler: {error}</p>}

      {!loading && !error && engagements.length === 0 && (
        <p className="text-gray-500">Noch keine Bewerbungen.</p>
      )}

      {!loading && !error && engagements.length > 0 && (
        <ul className="space-y-3">
          {engagements.map(e => (
            <li key={e.id} className="rounded border p-4">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="text-sm font-mono text-gray-500 text-xs">Freiwilliger: {e.volunteerId}</p>
                  {e.message && (
                    <p className="mt-1 text-sm text-gray-700">"{e.message}"</p>
                  )}
                  {e.timeSlotId && (
                    <p className="mt-1 text-xs text-gray-400">Zeitslot: {e.timeSlotId}</p>
                  )}
                  <p className="mt-1 text-xs text-gray-400">
                    Eingegangen: {new Date(e.createdOn).toLocaleDateString("de-DE")}
                  </p>
                </div>
                <div className="flex flex-col items-end gap-2 shrink-0">
                  <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600"}`}>
                    {STATUS_LABELS[e.status] ?? e.status}
                  </span>
                  {e.status === "Pending" && (
                    <div className="flex gap-2">
                      <button
                        onClick={() => handleConfirm(e.id)}
                        disabled={processing === e.id + "-confirm"}
                        className="text-xs rounded bg-green-600 px-2 py-1 text-white hover:bg-green-700 disabled:opacity-50"
                      >
                        {processing === e.id + "-confirm" ? "…" : "Bestätigen"}
                      </button>
                      <button
                        onClick={() => handleCancel(e.id)}
                        disabled={processing === e.id + "-cancel"}
                        className="text-xs rounded bg-red-600 px-2 py-1 text-white hover:bg-red-700 disabled:opacity-50"
                      >
                        {processing === e.id + "-cancel" ? "…" : "Absagen"}
                      </button>
                    </div>
                  )}
                  {e.status === "Confirmed" && (
                    <button
                      onClick={() => handleCancel(e.id)}
                      disabled={processing === e.id + "-cancel"}
                      className="text-xs text-red-600 hover:underline disabled:opacity-50"
                    >
                      {processing === e.id + "-cancel" ? "…" : "Stornieren"}
                    </button>
                  )}
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
