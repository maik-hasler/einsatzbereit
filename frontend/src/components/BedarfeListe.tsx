import { useEffect, useState } from "react";
import type { PagedListOfBedarf, Bedarf } from "../client/api-client";
import CreateBedarfModal from "./CreateBedarfModal";

interface Props {
  isLoggedIn: boolean;
}

export default function BedarfeListe({ isLoggedIn }: Props) {
  const [data, setData] = useState<PagedListOfBedarf | null>(null);
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
      .then((json: PagedListOfBedarf) => setData(json))
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [page, refreshKey]);

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-semibold">Aktuelle Bedarfe</h2>
        {isLoggedIn && (
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
              {data.items.map((bedarf: Bedarf) => (
                <li key={bedarf.id?.value} className="rounded border p-4">
                  <strong className="block text-sm font-medium">{bedarf.title}</strong>
                  <p className="mt-1 text-sm text-gray-600">{bedarf.description}</p>
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

      {showModal && (
        <CreateBedarfModal
          onClose={() => setShowModal(false)}
          onSuccess={() => setRefreshKey((k) => k + 1)}
        />
      )}
    </div>
  );
}
