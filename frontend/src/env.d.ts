/// <reference types="astro/client" />

declare namespace App {
  interface Locals {
    user: Record<string, unknown> | null;
  }
}
