import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CreateBedarfModal from "../src/components/CreateBedarfModal";

describe("CreateBedarfModal", () => {
  const onClose = vi.fn();
  const onSuccess = vi.fn();
  const organisationId = "org-123";

  beforeEach(() => {
    vi.restoreAllMocks();
    onClose.mockReset();
    onSuccess.mockReset();
  });

  it("should render all form fields", () => {
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    expect(screen.getByText("Bedarf erstellen")).toBeInTheDocument();
    expect(screen.getByLabelText("Titel")).toBeInTheDocument();
    expect(screen.getByLabelText("Beschreibung")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Musterstraße")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("1a")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("12345")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Berlin")).toBeInTheDocument();
    expect(screen.getByLabelText("Einmalig")).toBeInTheDocument();
    expect(screen.getByLabelText("Regelmäßig")).toBeInTheDocument();
    expect(screen.getByText("Erstellen")).toBeInTheDocument();
    expect(screen.getByText("Abbrechen")).toBeInTheDocument();
  });

  it("should call onClose when cancel button is clicked", async () => {
    const user = userEvent.setup();
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    await user.click(screen.getByText("Abbrechen"));

    expect(onClose).toHaveBeenCalledOnce();
  });

  it("should call onClose when backdrop is clicked", async () => {
    const user = userEvent.setup();
    const { container } = render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    await user.click(container.firstElementChild!);

    expect(onClose).toHaveBeenCalledOnce();
  });

  it("should submit form with all fields on success", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve({}),
    });
    vi.stubGlobal("fetch", fetchMock);

    const user = userEvent.setup();
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    await user.type(screen.getByLabelText("Titel"), "Helfer gesucht");
    await user.type(screen.getByLabelText("Beschreibung"), "Für den Umzug");
    await user.type(screen.getByPlaceholderText("Musterstraße"), "Hauptstraße");
    await user.type(screen.getByPlaceholderText("1a"), "42");
    await user.type(screen.getByPlaceholderText("12345"), "54321");
    await user.type(screen.getByPlaceholderText("Berlin"), "München");
    await user.click(screen.getByLabelText("Regelmäßig"));
    await user.click(screen.getByText("Erstellen"));

    await waitFor(() => {
      expect(onSuccess).toHaveBeenCalledOnce();
      expect(onClose).toHaveBeenCalledOnce();
    });

    const body = JSON.parse(fetchMock.mock.calls[0][1].body);
    expect(body.title).toBe("Helfer gesucht");
    expect(body.description).toBe("Für den Umzug");
    expect(body.organisationId).toBe(organisationId);
    expect(body.strasse).toBe("Hauptstraße");
    expect(body.hausnummer).toBe("42");
    expect(body.plz).toBe("54321");
    expect(body.ort).toBe("München");
    expect(body.frequenz).toBe("Regelmaessig");
  });

  it("should default frequenz to Einmalig", () => {
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    const einmalig = screen.getByLabelText("Einmalig") as HTMLInputElement;
    expect(einmalig.checked).toBe(true);
  });

  it("should show loading state while submitting", async () => {
    let resolveFetch: (value: unknown) => void;
    const fetchMock = vi.fn().mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve;
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const user = userEvent.setup();
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    await user.type(screen.getByLabelText("Titel"), "Test");
    await user.type(screen.getByLabelText("Beschreibung"), "Test");
    await user.type(screen.getByPlaceholderText("Musterstraße"), "Straße");
    await user.type(screen.getByPlaceholderText("1a"), "1");
    await user.type(screen.getByPlaceholderText("12345"), "12345");
    await user.type(screen.getByPlaceholderText("Berlin"), "Ort");
    await user.click(screen.getByText("Erstellen"));

    expect(screen.getByText("Wird erstellt…")).toBeInTheDocument();

    resolveFetch!({ ok: true, status: 201, json: () => Promise.resolve({}) });
  });

  it("should display error on failed request", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({ ok: false, status: 500 }),
    );

    const user = userEvent.setup();
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    await user.type(screen.getByLabelText("Titel"), "Test");
    await user.type(screen.getByLabelText("Beschreibung"), "Test");
    await user.type(screen.getByPlaceholderText("Musterstraße"), "Straße");
    await user.type(screen.getByPlaceholderText("1a"), "1");
    await user.type(screen.getByPlaceholderText("12345"), "12345");
    await user.type(screen.getByPlaceholderText("Berlin"), "Ort");
    await user.click(screen.getByText("Erstellen"));

    await waitFor(() => {
      expect(screen.getByText("Fehler 500")).toBeInTheDocument();
    });

    expect(onSuccess).not.toHaveBeenCalled();
  });

  it("should redirect to login on 401", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({ ok: false, status: 401 }),
    );
    Object.defineProperty(window, "location", {
      writable: true,
      value: { href: "" },
    });

    const user = userEvent.setup();
    render(
      <CreateBedarfModal
        organisationId={organisationId}
        onClose={onClose}
        onSuccess={onSuccess}
      />,
    );

    await user.type(screen.getByLabelText("Titel"), "Test");
    await user.type(screen.getByLabelText("Beschreibung"), "Test");
    await user.type(screen.getByPlaceholderText("Musterstraße"), "Straße");
    await user.type(screen.getByPlaceholderText("1a"), "1");
    await user.type(screen.getByPlaceholderText("12345"), "12345");
    await user.type(screen.getByPlaceholderText("Berlin"), "Ort");
    await user.click(screen.getByText("Erstellen"));

    await waitFor(() => {
      expect(window.location.href).toBe("/api/login");
    });
  });
});
