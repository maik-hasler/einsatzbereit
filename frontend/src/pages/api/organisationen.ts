import type { APIRoute } from "astro";
import { createApiClient } from "../../client/api-instance";

export const GET: APIRoute = async ({ cookies }) => {
  try {
    const api = createApiClient(cookies);
    const organisationen = await api.getOrganisationen();
    return Response.json(organisationen);
  } catch {
    return new Response("Unauthorized", { status: 401 });
  }
};

export const POST: APIRoute = async ({ cookies, request }) => {
  try {
    const api = createApiClient(cookies);
    const body = await request.json();
    const organisation = await api.createOrganisation(body);
    return Response.json(organisation, { status: 201 });
  } catch {
    return new Response("Unauthorized", { status: 401 });
  }
};
