export function getActiveOrgId(): string | null {
  const match = document.cookie.match(/(?:^|;\s*)active-org=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : null;
}

export function setActiveOrgCookie(orgId: string): void {
  document.cookie = `active-org=${orgId};path=/;max-age=${60 * 60 * 24 * 365};samesite=lax`;
}
