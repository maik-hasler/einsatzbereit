import type { APIRoute } from "astro";
import { createApiClient } from "../../client/api-instance";

export const GET: APIRoute = async ({ cookies }) => {
  try {
    const api = createApiClient(cookies);
    const organizations = await api.getOrganizations();
    return Response.json(organizations);
  } catch {
    return new Response("Unauthorized", { status: 401 });
  }
};

export const POST: APIRoute = async ({ cookies, request }) => {
  try {
    const api = createApiClient(cookies);
    const body = await request.json();
    const organization = await api.createOrganization(body);
    return Response.json(organization, { status: 201 });
  } catch {
    return new Response("Unauthorized", { status: 401 });
  }
};
