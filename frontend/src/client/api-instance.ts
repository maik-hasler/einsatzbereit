import axios from 'axios';
import type { AxiosInstance } from 'axios';
import { EinsatzbereitApi } from './api-client';

const API_URL = import.meta.env.API_URL ?? 'http://localhost:5000';

export function createAxiosInstance(getToken?: () => string | undefined): AxiosInstance {
    const instance = axios.create({ baseURL: API_URL });

    if (getToken) {
        instance.interceptors.request.use((config) => {
            const token = getToken();
            if (token) {
                config.headers.Authorization = `Bearer ${token}`;
            }
            return config;
        });
    }

    return instance;
}

export function createApiClient(getToken?: () => string | undefined): EinsatzbereitApi {
    return new EinsatzbereitApi('', createAxiosInstance(getToken));
}
