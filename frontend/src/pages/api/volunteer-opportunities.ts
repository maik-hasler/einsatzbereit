import type { APIRoute } from "astro";
import { EinsatzbereitApi } from "../../client/api-client";
import { createApiClient } from "../../client/api-instance";
import { API_URL } from "astro:env/server";

export const GET: APIRoute = async ({ url }) => {
  const pageNumber = Number(url.searchParams.get("page") ?? "1");
  const pageSize = Number(url.searchParams.get("size") ?? "10");

  const api = new EinsatzbereitApi(API_URL, { fetch: globalThis.fetch });
  const opportunities = await api.getVolunteerOpportunities(pageNumber, pageSize);
  return Response.json(opportunities);
};

export const POST: APIRoute = async ({ cookies, request }) => {
  try {
    const api = createApiClient(cookies);
    const body = await request.json();
    const opportunity = await api.createVolunteerOpportunity(body);
    return Response.json(opportunity, { status: 201 });
  } catch {
    return new Response("Unauthorized", { status: 401 });
  }
};
