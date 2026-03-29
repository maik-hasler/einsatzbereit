import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import BedarfeListe from "../src/components/BedarfeListe";

const mockBedarfe = {
  totalItems: 2,
  currentPage: 1,
  pageCount: 1,
  items: [
    {
      id: "bedarf-1",
      title: "Helfer gesucht",
      description: "Wir brauchen Hilfe beim Umzug",
      organisationName: "Testorganisation",
      adresse: {
        strasse: "Musterstraße",
        hausnummer: "1",
        plz: "12345",
        ort: "Berlin",
      },
      frequenz: 0,
      status: "Entwurf",
      publishedOn: undefined,
      createdOn: "2026-03-29T10:00:00Z",
    },
    {
      id: "bedarf-2",
      title: "Regelmäßige Unterstützung",
      description: "Jeden Samstag helfen",
      organisationName: "Andere Organisation",
      adresse: {
        strasse: "Hauptstraße",
        hausnummer: "42",
        plz: "54321",
        ort: "München",
      },
      frequenz: 1,
      status: "Veröffentlicht",
      publishedOn: "2026-03-28T10:00:00Z",
      createdOn: "2026-03-28T08:00:00Z",
    },
  ],
};

function mockFetch(data = mockBedarfe) {
  return vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve(data),
  });
}

describe("BedarfeListe", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("should display bedarfe with all details", async () => {
    vi.stubGlobal("fetch", mockFetch());

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText("Helfer gesucht")).toBeInTheDocument();
    });

    expect(screen.getByText("Wir brauchen Hilfe beim Umzug")).toBeInTheDocument();
    expect(screen.getByText("Testorganisation")).toBeInTheDocument();
    expect(screen.getByText("Musterstraße 1, 12345 Berlin")).toBeInTheDocument();
    expect(screen.getByText("Entwurf")).toBeInTheDocument();
    expect(screen.getByText("Einmalig")).toBeInTheDocument();
  });

  it("should display regelmaessig frequenz correctly", async () => {
    vi.stubGlobal("fetch", mockFetch());

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText("Regelmäßige Unterstützung")).toBeInTheDocument();
    });

    expect(screen.getByText("Regelmäßig")).toBeInTheDocument();
    expect(screen.getByText("Veröffentlicht")).toBeInTheDocument();
    expect(screen.getByText("Andere Organisation")).toBeInTheDocument();
    expect(screen.getByText("Hauptstraße 42, 54321 München")).toBeInTheDocument();
  });

  it("should show empty state when no bedarfe exist", async () => {
    vi.stubGlobal(
      "fetch",
      mockFetch({ totalItems: 0, currentPage: 1, pageCount: 0, items: [] }),
    );

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText("Keine Bedarfe gefunden.")).toBeInTheDocument();
    });
  });

  it("should show loading state", () => {
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise(() => {})));

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    expect(screen.getByText("Wird geladen…")).toBeInTheDocument();
  });

  it("should show error state on fetch failure", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({ ok: false, status: 500 }),
    );

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText("Fehler: Fehler 500")).toBeInTheDocument();
    });
  });

  it("should show create button when canCreateBedarf is true", () => {
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise(() => {})));

    render(<BedarfeListe canCreateBedarf={true} activeOrgId="org-1" />);

    expect(screen.getByText("+ Bedarf erstellen")).toBeInTheDocument();
  });

  it("should not show create button when canCreateBedarf is false", () => {
    vi.stubGlobal("fetch", vi.fn().mockReturnValue(new Promise(() => {})));

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    expect(screen.queryByText("+ Bedarf erstellen")).not.toBeInTheDocument();
  });

  it("should show pagination when multiple pages exist", async () => {
    vi.stubGlobal(
      "fetch",
      mockFetch({
        totalItems: 20,
        currentPage: 1,
        pageCount: 2,
        items: mockBedarfe.items,
      }),
    );

    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText("1 / 2")).toBeInTheDocument();
    });

    expect(screen.getByText("← Zurück")).toBeInTheDocument();
    expect(screen.getByText("Weiter →")).toBeInTheDocument();
  });

  it("should navigate to next page when clicking Weiter", async () => {
    const fetchMock = mockFetch({
      totalItems: 20,
      currentPage: 1,
      pageCount: 2,
      items: mockBedarfe.items,
    });
    vi.stubGlobal("fetch", fetchMock);

    const user = userEvent.setup();
    render(<BedarfeListe canCreateBedarf={false} activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText("Weiter →")).toBeInTheDocument();
    });

    await user.click(screen.getByText("Weiter →"));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith("/api/bedarfe?page=2&size=10");
    });
  });
});
