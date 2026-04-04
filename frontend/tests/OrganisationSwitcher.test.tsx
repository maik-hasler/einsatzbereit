import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import OrganizationSwitcher from '../src/components/OrganizationSwitcher';

const mockOrgs = [
  { id: 'org-1', name: 'Feuerwehr Musterstadt' },
  { id: 'org-2', name: 'THW Ortsverband' },
];

describe('OrganisationSwitcher', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    document.cookie = 'active-org=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
  });

  function mockFetch(orgs: Array<{ id: string; name: string }> = mockOrgs) {
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url === '/api/organisationen') {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve(orgs),
        });
      }
      return Promise.resolve({ ok: false, status: 404 });
    });
    vi.stubGlobal('fetch', fetchMock);
    return fetchMock;
  }

  it('should show loading skeleton initially', () => {
    // Don't resolve fetch immediately
    vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})));

    render(<OrganizationSwitcher activeOrgId={null} />);

    // Loading skeleton has animate-pulse class
    const skeleton = document.querySelector('.animate-pulse');
    expect(skeleton).toBeInTheDocument();
  });

  it('should display organisations after loading', async () => {
    mockFetch();

    // Mock reload to prevent jsdom errors
    Object.defineProperty(window, 'location', {
      value: { reload: vi.fn(), href: '' },
      writable: true,
    });

    render(<OrganizationSwitcher activeOrgId="org-1" />);

    await waitFor(() => {
      expect(screen.getByText('Feuerwehr Musterstadt')).toBeInTheDocument();
    });
  });

  it('should show dropdown with all orgs when button is clicked', async () => {
    const user = userEvent.setup();
    mockFetch();

    Object.defineProperty(window, 'location', {
      value: { reload: vi.fn(), href: '' },
      writable: true,
    });

    render(<OrganizationSwitcher activeOrgId="org-1" />);

    await waitFor(() => {
      expect(screen.getByText('Feuerwehr Musterstadt')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Organisation wechseln' }));

    expect(screen.getByText('THW Ortsverband')).toBeInTheDocument();
    expect(screen.getByText('Organisation erstellen')).toBeInTheDocument();
  });

  it('should set cookie and reload when switching organisation', async () => {
    const user = userEvent.setup();
    mockFetch();

    const reloadMock = vi.fn();
    Object.defineProperty(window, 'location', {
      value: { reload: reloadMock, href: '' },
      writable: true,
    });

    render(<OrganizationSwitcher activeOrgId="org-1" />);

    await waitFor(() => {
      expect(screen.getByText('Feuerwehr Musterstadt')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Organisation wechseln' }));
    await user.click(screen.getByText('THW Ortsverband'));

    expect(document.cookie).toContain('active-org=org-2');
    expect(reloadMock).toHaveBeenCalled();
  });

  it('should show "Organisation wählen" when no active org', async () => {
    mockFetch([]);

    render(<OrganizationSwitcher activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText('Organisation wählen')).toBeInTheDocument();
    });
  });

  it('should open create modal when "Organisation erstellen" is clicked', async () => {
    const user = userEvent.setup();
    mockFetch();

    Object.defineProperty(window, 'location', {
      value: { reload: vi.fn(), href: '' },
      writable: true,
    });

    render(<OrganizationSwitcher activeOrgId="org-1" />);

    await waitFor(() => {
      expect(screen.getByText('Feuerwehr Musterstadt')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Organisation wechseln' }));
    await user.click(screen.getByText('Organisation erstellen'));

    // Modal should appear
    expect(screen.getByText('Erstellen')).toBeInTheDocument();
    expect(screen.getByText('Abbrechen')).toBeInTheDocument();
  });

  it('should handle fetch error gracefully', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Network error')));

    render(<OrganizationSwitcher activeOrgId={null} />);

    await waitFor(() => {
      expect(screen.getByText('Organisation wählen')).toBeInTheDocument();
    });
  });

  it('should close dropdown when clicking outside', async () => {
    const user = userEvent.setup();
    mockFetch();

    Object.defineProperty(window, 'location', {
      value: { reload: vi.fn(), href: '' },
      writable: true,
    });

    render(
      <div>
        <span data-testid="outside">Outside</span>
        <OrganizationSwitcher activeOrgId="org-1" />
      </div>
    );

    await waitFor(() => {
      expect(screen.getByText('Feuerwehr Musterstadt')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Organisation wechseln' }));
    expect(screen.getByText('THW Ortsverband')).toBeInTheDocument();

    await user.click(screen.getByTestId('outside'));

    await waitFor(() => {
      expect(screen.queryByText('THW Ortsverband')).not.toBeInTheDocument();
    });
  });
});
