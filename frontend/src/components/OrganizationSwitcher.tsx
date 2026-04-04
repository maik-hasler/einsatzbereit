import { useEffect, useRef, useState } from "react";
import type { KeycloakOrganization } from "../client/api-client";
import CreateOrganizationModal from "./CreateOrganizationModal";

interface Props {
  activeOrgId: string | null;
}

function setActiveOrgCookie(orgId: string) {
  document.cookie = `active-org=${orgId};path=/;max-age=${60 * 60 * 24 * 365};samesite=lax`;
}

export default function OrganizationSwitcher({ activeOrgId }: Props) {
  const [orgs, setOrgs] = useState<KeycloakOrganization[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const activeOrg = orgs.find((o) => o.id === activeOrgId) ?? null;

  const fetchOrgs = () => {
    setLoading(true);
    fetch("/api/organizations")
      .then((res) => {
        if (!res.ok) throw new Error(`Fehler ${res.status}`);
        return res.json();
      })
      .then((data: KeycloakOrganization[]) => {
        setOrgs(data);

        // Auto-select first org if none is active
        if (!activeOrgId && data.length > 0) {
          setActiveOrgCookie(data[0].id);
          window.location.reload();
        }
      })
      .catch(() => setOrgs([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchOrgs();
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
    setOpen(false);
    window.location.reload();
  };

  const handleOrgCreated = () => {
    fetchOrgs();
    // Reload to pick up the new org and potentially new role
    setTimeout(() => window.location.reload(), 300);
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
