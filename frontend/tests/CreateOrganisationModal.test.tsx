import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import CreateOrganizationModal from '../src/components/CreateOrganizationModal';

describe('CreateOrganisationModal', () => {
  const onClose = vi.fn();
  const onSuccess = vi.fn();

  beforeEach(() => {
    vi.restoreAllMocks();
    onClose.mockClear();
    onSuccess.mockClear();
  });

  it('should render the form with name input and submit button', () => {
    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    expect(screen.getByText('Organisation erstellen')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('z.B. Freiwillige Feuerwehr Musterstadt')).toBeInTheDocument();
    expect(screen.getByText('Erstellen')).toBeInTheDocument();
    expect(screen.getByText('Abbrechen')).toBeInTheDocument();
  });

  it('should call onClose when cancel button is clicked', async () => {
    const user = userEvent.setup();
    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    await user.click(screen.getByText('Abbrechen'));

    expect(onClose).toHaveBeenCalledOnce();
  });

  it('should call onClose when backdrop is clicked', async () => {
    const user = userEvent.setup();
    const { container } = render(
      <CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />
    );

    // Click the backdrop (outermost div)
    const backdrop = container.firstElementChild as HTMLElement;
    await user.click(backdrop);

    expect(onClose).toHaveBeenCalled();
  });

  it('should submit the form and call onSuccess on successful creation', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve({ name: 'Test Org' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    await user.type(
      screen.getByPlaceholderText('z.B. Freiwillige Feuerwehr Musterstadt'),
      'Feuerwehr Musterstadt'
    );
    await user.click(screen.getByText('Erstellen'));

    await waitFor(() => {
      expect(onSuccess).toHaveBeenCalledOnce();
      expect(onClose).toHaveBeenCalledOnce();
    });

    expect(fetchMock).toHaveBeenCalledWith('/api/organisationen', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'Feuerwehr Musterstadt' }),
    });
  });

  it('should show loading state while submitting', async () => {
    const user = userEvent.setup();
    let resolveRequest!: (value: Response) => void;
    const fetchMock = vi.fn().mockReturnValue(
      new Promise<Response>((resolve) => { resolveRequest = resolve; })
    );
    vi.stubGlobal('fetch', fetchMock);

    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    await user.type(
      screen.getByPlaceholderText('z.B. Freiwillige Feuerwehr Musterstadt'),
      'Test'
    );
    await user.click(screen.getByText('Erstellen'));

    expect(screen.getByText('Wird erstellt…')).toBeInTheDocument();

    resolveRequest({ ok: true, status: 201, json: () => Promise.resolve({}) } as Response);

    await waitFor(() => {
      expect(onSuccess).toHaveBeenCalled();
    });
  });

  it('should display error message on failed request', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    await user.type(
      screen.getByPlaceholderText('z.B. Freiwillige Feuerwehr Musterstadt'),
      'Test'
    );
    await user.click(screen.getByText('Erstellen'));

    await waitFor(() => {
      expect(screen.getByText('Fehler 500')).toBeInTheDocument();
    });

    expect(onSuccess).not.toHaveBeenCalled();
  });

  it('should redirect to login on 401 response', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
    });
    vi.stubGlobal('fetch', fetchMock);

    const locationMock = { href: '' };
    Object.defineProperty(window, 'location', {
      value: locationMock,
      writable: true,
    });

    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    await user.type(
      screen.getByPlaceholderText('z.B. Freiwillige Feuerwehr Musterstadt'),
      'Test'
    );
    await user.click(screen.getByText('Erstellen'));

    await waitFor(() => {
      expect(locationMock.href).toBe('/api/login');
    });
  });

  it('should handle names with German special characters', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve({ name: 'Ärztlicher Übungsdienst' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    render(<CreateOrganizationModal onClose={onClose} onSuccess={onSuccess} />);

    await user.type(
      screen.getByPlaceholderText('z.B. Freiwillige Feuerwehr Musterstadt'),
      'Ärztlicher Übungsdienst'
    );
    await user.click(screen.getByText('Erstellen'));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/organisationen', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: 'Ärztlicher Übungsdienst' }),
      });
    });
  });
});
