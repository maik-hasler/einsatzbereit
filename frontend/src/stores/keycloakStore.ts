import { defineStore } from 'pinia';
import Keycloak, { KeycloakTokenParsed } from 'keycloak-js';

export const useKeycloakStore = defineStore('keycloak', {
  state: () => ({
    keycloak: null as Keycloak | null,
    authenticated: false,
    token: null as string | null,
    tokenParsed: null as KeycloakTokenParsed | null,
    tenant: null as string | null
  }),
  actions: {
    async initKeycloak(config: { url: string; realm: string; clientId: string }) {
      const kc = new Keycloak(config);
      this.keycloak = kc;
      this.tenant = config.realm;

      const authenticated = await kc.init({ onLoad: 'login-required' });
      this.authenticated = authenticated;

      if (authenticated) {
        this.updateTokens();
        this.startTokenRefresh();
      }

      return authenticated;
    },
    updateTokens() {
      if (!this.keycloak) return;
      this.token = this.keycloak.token;
      this.tokenParsed = this.keycloak.tokenParsed;
    },
    startTokenRefresh() {
      if (!this.keycloak) return;
      setInterval(async () => {
        const refreshed = await this.keycloak!.updateToken(60);
        if (refreshed) this.updateTokens();
      }, 60000);
    },
    logout() {
      this.keycloak?.logout();
      this.token = null;
      this.tokenParsed = null;
      this.authenticated = false;
    }
  },
  getters: {
    authHeader: (state) => (state.token ? `Bearer ${state.token}` : ''),
    hasRole: (state) => (role: string) => state.tokenParsed?.roles?.includes(role) ?? false,
    username: (state) => state.tokenParsed?.preferred_username ?? ''
  }
});