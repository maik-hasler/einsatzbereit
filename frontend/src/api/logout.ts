import type { APIRoute } from "astro";

export const GET: APIRoute = async ({ cookies, redirect }) => {
  cookies.delete("session", { path: "/" });
  cookies.delete("code_verifier", { path: "/" });
  return redirect("/");
};
