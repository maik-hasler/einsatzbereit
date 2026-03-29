import { useEffect, useState } from "react";
import type { PagedListOfBedarfSummary, BedarfSummary } from "../client/api-client";
import CreateBedarfModal from "./CreateBedarfModal";

interface Props {
  canCreateBedarf: boolean;
  activeOrgId: string | null;
}

function formatFrequenz(frequenz: number): string {
  return frequenz === 1 ? "Regelmäßig" : "Einmalig";
}

export default function BedarfeListe({ canCreateBedarf, activeOrgId }: Props) {
  const [data, setData] = useState<PagedListOfBedarfSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [refreshKey, setRefreshKey] = useState(0);
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    setLoading(true);
    setError(null);

    fetch(`/api/bedarfe?page=${page}&size=10`)
      .then((res) => {
        if (!res.ok) throw new Error(`Fehler ${res.status}`);
        return res.json();
      })
      .then((json: PagedListOfBedarfSummary) => setData(json))
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [page, refreshKey]);

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-semibold">Aktuelle Bedarfe</h2>
        {canCreateBedarf && (
          <button
            onClick={() => setShowModal(true)}
            className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
          >
            + Bedarf erstellen
          </button>
        )}
      </div>

      {loading && <p className="text-gray-500">Wird geladen…</p>}
      {error && <p className="text-red-600">Fehler: {error}</p>}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <p className="text-gray-500">Keine Bedarfe gefunden.</p>
          ) : (
            <ul className="space-y-3">
              {data.items.map((bedarf: BedarfSummary) => (
                <li key={bedarf.id} className="rounded border p-4">
                  <div className="flex items-start justify-between">
                    <div>
                      <strong className="block text-sm font-medium">{bedarf.title}</strong>
                      <p className="mt-1 text-sm text-gray-600">{bedarf.description}</p>
                    </div>
                    <div className="flex gap-2">
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                        {bedarf.status}
                      </span>
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                        {formatFrequenz(bedarf.frequenz)}
                      </span>
                    </div>
                  </div>
                  <div className="mt-2 flex items-center gap-4 text-xs text-gray-500">
                    <span>{bedarf.organisationName}</span>
                    <span>
                      {bedarf.adresse.strasse} {bedarf.adresse.hausnummer},{" "}
                      {bedarf.adresse.plz} {bedarf.adresse.ort}
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}

          {(data.pageCount ?? 1) > 1 && (
            <div className="mt-4 flex items-center gap-3">
              <button
                onClick={() => setPage((p) => p - 1)}
                disabled={page <= 1}
                className="rounded px-3 py-1 text-sm hover:bg-gray-100 disabled:opacity-40"
              >
                ← Zurück
              </button>
              <span className="text-sm text-gray-500">{page} / {data.pageCount}</span>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= (data.pageCount ?? 1)}
                className="rounded px-3 py-1 text-sm hover:bg-gray-100 disabled:opacity-40"
              >
                Weiter →
              </button>
            </div>
          )}
        </>
      )}

      {showModal && activeOrgId && (
        <CreateBedarfModal
          organisationId={activeOrgId}
          onClose={() => setShowModal(false)}
          onSuccess={() => setRefreshKey((k) => k + 1)}
        />
      )}
    </div>
  );
}
