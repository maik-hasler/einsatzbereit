import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import type { KeycloakOrganization } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import CreateOrganizationModal from "./CreateOrganizationModal";

function getActiveOrgId(): string | null {
  const match = document.cookie.match(/(?:^|;\s*)active-org=([^;]*)/)
  return match ? decodeURIComponent(match[1]) : null
}

function setActiveOrgCookie(orgId: string) {
  document.cookie = `active-org=${orgId};path=/;max-age=${60 * 60 * 24 * 365};samesite=lax`;
}

export default function OrganizationSwitcher() {
  const api = useApiClient();
  const navigate = useNavigate();
  const [orgs, setOrgs] = useState<KeycloakOrganization[]>([]);
  const [activeOrgId, setActiveOrgId] = useState<string | null>(getActiveOrgId);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const activeOrg = orgs.find((o) => o.id === activeOrgId) ?? null;

  const fetchOrgs = () => {
    setLoading(true);
    api.getOrganizations()
      .then((data: KeycloakOrganization[]) => {
        setOrgs(data);
        // Auto-select first org if none is active
        if (!getActiveOrgId() && data.length > 0) {
          setActiveOrgCookie(data[0].id);
          setActiveOrgId(data[0].id);
        }
      })
      .catch(() => setOrgs([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchOrgs();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Close dropdown on outside click
  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("click", handleClick);
    return () => document.removeEventListener("click", handleClick);
  }, []);

  const handleSwitch = (org: KeycloakOrganization) => {
    setActiveOrgCookie(org.id);
    setActiveOrgId(org.id);
    setOpen(false);
  };

  const handleOrgCreated = () => {
    const prevIds = new Set(orgs.map((o) => o.id));
    setLoading(true);
    api.getOrganizations()
      .then((data: KeycloakOrganization[]) => {
        setOrgs(data);
        const newOrg = data.find((o) => !prevIds.has(o.id));
        if (newOrg) {
          setActiveOrgCookie(newOrg.id);
          setActiveOrgId(newOrg.id);
        } else if (!getActiveOrgId() && data.length > 0) {
          setActiveOrgCookie(data[0].id);
          setActiveOrgId(data[0].id);
        }
      })
      .catch(() => setOrgs([]))
      .finally(() => setLoading(false));
  };

  if (loading) {
    return (
      <div className="h-9 w-32 animate-pulse rounded-lg bg-gray-100" />
    );
  }

  // No orgs — show direct "create" button instead of dropdown
  if (orgs.length === 0) {
    return (
      <>
        <button
          type="button"
          onClick={() => setShowModal(true)}
          data-testid="create-org-btn"
          className="flex items-center gap-2 rounded-lg border border-dashed border-brand-300 bg-brand-50 px-3 py-1.5 text-sm font-medium text-brand-600 hover:bg-brand-100 transition-colors"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          Organisation erstellen
        </button>

        {showModal && (
          <CreateOrganizationModal
            onClose={() => setShowModal(false)}
            onSuccess={handleOrgCreated}
          />
        )}
      </>
    );
  }

  return (
    <>
      <div className="relative" ref={containerRef}>
        <button
          type="button"
          onClick={() => setOpen(!open)}
          className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
          aria-expanded={open}
          aria-label="Organisation wechseln"
        >
          {/* Building icon */}
          <svg className="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 21h19.5m-18-18v18m10.5-18v18m6-13.5V21M6.75 6.75h.75m-.75 3h.75m-.75 3h.75m3-6h.75m-.75 3h.75m-.75 3h.75M6.75 21v-3.375c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21M3 3h12m-.75 4.5H21m-3.75 3H21m-3.75 3H21" />
          </svg>

          <span className="max-w-[150px] truncate">
            {activeOrg ? activeOrg.name : "Organisation wählen"}
          </span>

          {/* Chevron */}
          <svg className={`w-3.5 h-3.5 text-gray-400 transition-transform ${open ? "rotate-180" : ""}`} fill="none" viewBox="0 0 24 24" strokeWidth="2" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
          </svg>
        </button>

        {/* Dropdown */}
        {open && (
          <div className="absolute left-0 top-full mt-2 w-64 rounded-lg bg-white border border-gray-200 shadow-lg z-50">
            <div className="py-1 max-h-60 overflow-y-auto">
              {orgs.map((org) => (
                <button
                  key={org.id}
                  type="button"
                  onClick={() => handleSwitch(org)}
                  className={`flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors ${
                    org.id === activeOrgId
                      ? "bg-brand-50 text-brand-700 font-medium"
                      : "text-gray-700 hover:bg-gray-50"
                  }`}
                >
                  <span className="flex h-7 w-7 items-center justify-center rounded-md bg-brand-100 text-xs font-semibold text-brand-600">
                    {org.name.charAt(0).toUpperCase()}
                  </span>
                  <span className="truncate">{org.name}</span>
                  {org.id === activeOrgId && (
                    <svg className="ml-auto w-4 h-4 text-brand-500" fill="none" viewBox="0 0 24 24" strokeWidth="2" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                    </svg>
                  )}
                </button>
              ))}
            </div>

            <div className="border-t border-gray-100">
              {activeOrgId && (
                <button
                  type="button"
                  data-testid="org-settings-link"
                  onClick={() => {
                    setOpen(false);
                    navigate(`/organizations/${activeOrgId}/settings`);
                  }}
                  className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
                >
                  <svg className="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z" />
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
                  </svg>
                  Einstellungen
                </button>
              )}
              <button
                type="button"
                onClick={() => {
                  setOpen(false);
                  setShowModal(true);
                }}
                className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-brand-600 hover:bg-brand-50 transition-colors"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                </svg>
                Organisation erstellen
              </button>
            </div>
          </div>
        )}
      </div>

      {showModal && (
        <CreateOrganizationModal
          onClose={() => setShowModal(false)}
          onSuccess={handleOrgCreated}
        />
      )}
    </>
  );
}
