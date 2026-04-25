import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import type { PagedListOfVolunteerOpportunitySummary, VolunteerOpportunitySummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getActiveOrgId } from "../lib/activeOrg";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";

interface Props {
  canCreateOpportunity: boolean;
}

export default function VolunteerOpportunitiesList({ canCreateOpportunity }: Props) {
  const api = useApiClient();
  const navigate = useNavigate();
  const [data, setData] = useState<PagedListOfVolunteerOpportunitySummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [refreshKey, setRefreshKey] = useState(0);
  const [showModal, setShowModal] = useState(false);

  const [search, setSearch] = useState("");
  const [city, setCity] = useState("");
  const [occurrence, setOccurrence] = useState("");
  const [participationType, setParticipationType] = useState("");

  useEffect(() => {
    setLoading(true);
    setError(null);

    api.getVolunteerOpportunities(
      page,
      10,
      search || undefined,
      city || undefined,
      occurrence || undefined,
      participationType || undefined,
    )
      .then((json: PagedListOfVolunteerOpportunitySummary) => setData(json))
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, refreshKey, search, city, occurrence, participationType]);

  const activeOrgId = getActiveOrgId();

  function handleFilterChange() {
    setPage(1);
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-semibold">Aktuelle Bedarfe</h2>
        {canCreateOpportunity && (
          <button
            onClick={() => setShowModal(true)}
            data-testid="create-opportunity-btn"
            className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
          >
            + Bedarf erstellen
          </button>
        )}
      </div>

      <div className="mb-4 flex flex-wrap gap-2">
        <input
          type="text"
          placeholder="Suche…"
          value={search}
          onChange={e => { setSearch(e.target.value); handleFilterChange(); }}
          className="rounded border px-3 py-1.5 text-sm"
        />
        <input
          type="text"
          placeholder="Stadt…"
          value={city}
          onChange={e => { setCity(e.target.value); handleFilterChange(); }}
          className="rounded border px-3 py-1.5 text-sm"
        />
        <select
          value={occurrence}
          onChange={e => { setOccurrence(e.target.value); handleFilterChange(); }}
          className="rounded border px-3 py-1.5 text-sm text-gray-700"
        >
          <option value="">Alle Häufigkeiten</option>
          <option value="OneTime">Einmalig</option>
          <option value="Recurring">Regelmäßig</option>
        </select>
        <select
          value={participationType}
          onChange={e => { setParticipationType(e.target.value); handleFilterChange(); }}
          className="rounded border px-3 py-1.5 text-sm text-gray-700"
        >
          <option value="">Alle Typen</option>
          <option value="Waitlist">Warteliste</option>
          <option value="IndividualContact">Einzelkontakt</option>
        </select>
      </div>

      {loading && <p className="text-gray-500">Wird geladen…</p>}
      {error && <p className="text-red-600">Fehler: {error}</p>}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <p className="text-gray-500">Keine Bedarfe gefunden.</p>
          ) : (
            <ul className="space-y-3">
              {data.items.map((item: VolunteerOpportunitySummary) => (
                <li
                  key={item.id}
                  className="cursor-pointer rounded border p-4 hover:bg-gray-50 transition-colors"
                  onClick={() => navigate(`/volunteer-opportunities/${item.id}`)}
                >
                  <div className="flex items-start justify-between">
                    <div>
                      <strong className="block text-sm font-medium">{item.title}</strong>
                      <p className="mt-1 text-sm text-gray-600">{item.description}</p>
                    </div>
                    <div className="flex flex-col items-end gap-1 shrink-0 ml-2">
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                        {formatOccurrence(item.occurrence)}
                      </span>
                      <span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs text-blue-700">
                        {formatParticipationType(item.participationType)}
                      </span>
                    </div>
                  </div>
                  <div className="mt-2 flex items-center gap-4 text-xs text-gray-500">
                    <span>{item.organizationName}</span>
                    {item.isRemote ? (
                      <span>Remote</span>
                    ) : (
                      <span>
                        {item.street} {item.houseNumber},{" "}
                        {item.zipCode} {item.city}
                      </span>
                    )}
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
        <CreateVolunteerOpportunityModal
          organizationId={activeOrgId}
          onClose={() => setShowModal(false)}
          onSuccess={() => setRefreshKey((k) => k + 1)}
        />
      )}
    </div>
  );
}
