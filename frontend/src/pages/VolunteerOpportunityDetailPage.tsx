import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";
import type { VolunteerOpportunityDetails } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { formatDateTime, formatOccurrence, formatParticipationType } from "../lib/format";
import SignUpModal from "../components/SignUpModal";
import EditVolunteerOpportunityModal from "../components/EditVolunteerOpportunityModal";

export default function VolunteerOpportunityDetailPage() {
  const { opportunityId } = useParams<{ opportunityId: string }>();
  const navigate = useNavigate();
  const auth = useAuth();
  const api = useApiClient();

  const [opportunity, setOpportunity] = useState<VolunteerOpportunityDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showSignUp, setShowSignUp] = useState(false);
  const [signedUp, setSignedUp] = useState(false);
  const [showEdit, setShowEdit] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const roles = (Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []) as string[];
  const isOrganisator = roles.includes("organisator");

  useEffect(() => {
    if (!opportunityId) return;
    load();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opportunityId]);

  function load() {
    if (!opportunityId) return;
    setLoading(true);
    api.getVolunteerOpportunityDetails(opportunityId)
      .then(setOpportunity)
      .catch(err => setError(err.message))
      .finally(() => setLoading(false));
  }

  async function handleDelete() {
    if (!opportunityId || !confirm("Bedarf wirklich löschen?")) return;
    setDeleting(true);
    try {
      await api.deleteVolunteerOpportunity(opportunityId);
      navigate("/");
    } catch (err) {
      alert(err instanceof Error ? err.message : "Fehler beim Löschen");
      setDeleting(false);
    }
  }

  if (loading) return <p className="text-gray-500">Wird geladen…</p>;
  if (error) return <p className="text-red-600">Fehler: {error}</p>;
  if (!opportunity) return <p className="text-gray-500">Nicht gefunden.</p>;

  const isAuthenticated = auth.isAuthenticated;

  return (
    <div className="max-w-2xl">
      <button
        onClick={() => navigate(-1)}
        className="mb-4 text-sm text-gray-500 hover:text-gray-800"
      >
        ← Zurück
      </button>

      <div className="mb-1 flex items-start justify-between gap-4">
        <h1 className="text-2xl font-bold text-gray-900">{opportunity.title}</h1>
        {isOrganisator && (
          <div className="flex gap-2 shrink-0">
            <button
              onClick={() => setShowEdit(true)}
              className="rounded border px-3 py-1 text-sm text-gray-600 hover:bg-gray-50"
            >
              Bearbeiten
            </button>
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="rounded border border-red-200 px-3 py-1 text-sm text-red-600 hover:bg-red-50 disabled:opacity-50"
            >
              {deleting ? "…" : "Löschen"}
            </button>
          </div>
        )}
      </div>

      <p className="mb-4 text-sm text-gray-500">{opportunity.organizationName}</p>

      <p className="mb-6 text-gray-700">{opportunity.description}</p>

      <div className="mb-6 flex flex-wrap gap-2">
        <span className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700">
          {formatOccurrence(opportunity.occurrence)}
        </span>
        <span className="rounded-full bg-blue-50 px-3 py-1 text-sm text-blue-700">
          {formatParticipationType(opportunity.participationType)}
        </span>
        {opportunity.isRemote ? (
          <span className="rounded-full bg-green-50 px-3 py-1 text-sm text-green-700">Remote</span>
        ) : (
          <span className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700">
            {opportunity.street} {opportunity.houseNumber}, {opportunity.zipCode} {opportunity.city}
          </span>
        )}
      </div>

      {opportunity.participationType === "Waitlist" && opportunity.timeSlots.length > 0 && (
        <div className="mb-6">
          <h2 className="mb-2 text-sm font-semibold text-gray-700">Verfügbare Zeitslots</h2>
          <ul className="space-y-2">
            {opportunity.timeSlots.map(ts => (
              <li key={ts.id} className="rounded border px-3 py-2 text-sm text-gray-700">
                {formatDateTime(ts.startDateTime as unknown as string)} – {formatDateTime(ts.endDateTime as unknown as string)}
                <span className="ml-2 text-gray-400">(max. {ts.maxParticipants} Personen)</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {isOrganisator && (
        <div className="mb-6">
          <button
            onClick={() => navigate(`/volunteer-opportunities/${opportunityId}/engagements`)}
            className="text-sm text-blue-600 hover:underline"
          >
            Bewerbungen verwalten →
          </button>
        </div>
      )}

      {isAuthenticated && !isOrganisator && !signedUp && (
        <button
          onClick={() => setShowSignUp(true)}
          className="rounded bg-black px-5 py-2 text-sm text-white hover:bg-gray-800"
        >
          {opportunity.participationType === "Waitlist" ? "Auf Warteliste eintragen" : "Interesse bekunden"}
        </button>
      )}

      {!isAuthenticated && (
        <p className="text-sm text-gray-500">
          Bitte{" "}
          <button
            onClick={() => auth.signinRedirect()}
            className="underline hover:text-gray-800"
          >
            anmelden
          </button>
          , um dich zu bewerben.
        </p>
      )}

      {signedUp && (
        <p className="rounded bg-green-50 px-4 py-2 text-sm text-green-700">
          Anmeldung erfolgreich! Deine Bewerbung wurde übermittelt.
        </p>
      )}

      {showSignUp && (
        <SignUpModal
          opportunityId={opportunity.id}
          participationType={opportunity.participationType}
          timeSlots={opportunity.timeSlots}
          onClose={() => setShowSignUp(false)}
          onSuccess={() => setSignedUp(true)}
        />
      )}

      {showEdit && (
        <EditVolunteerOpportunityModal
          opportunity={opportunity}
          onClose={() => setShowEdit(false)}
          onSuccess={load}
        />
      )}
    </div>
  );
}
