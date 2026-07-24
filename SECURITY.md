# Security Policy

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities in
the application itself (as opposed to a flagged dependency, which the
[dependency security scan](.github/workflows/security.yml) already surfaces
publicly).

Instead, use GitHub's private vulnerability reporting for this repository:
open the [Security tab](../../security) and click **"Report a vulnerability"**.
This opens a private advisory visible only to you and the maintainer, with no
public disclosure until a fix is ready.

If you're unable to use that flow, contact [@maik-hasler](https://github.com/maik-hasler)
directly via GitHub.

## What to Include

- A description of the vulnerability and its impact
- Steps to reproduce (a minimal example helps most)
- Affected component (backend, frontend, keycloak) and version/commit if known

## Response

This is a volunteer-run open source project without a fixed SLA, but reports
are reviewed as soon as possible. You'll get an acknowledgment, and a fix or
mitigation once the issue is understood. Credit is given in the fix's release
notes unless you'd prefer to stay anonymous.

## Scope

`Dependency Security Scan` (`.github/workflows/security.yml`) already audits
NuGet and npm dependencies for known CVEs on a weekly schedule and on
pull requests/pushes that touch dependency manifests. Use this policy for
vulnerabilities in Einsatzbereit's own code (backend, frontend, or the
Keycloak realm configuration), not for dependency CVEs already caught by
that scan.
