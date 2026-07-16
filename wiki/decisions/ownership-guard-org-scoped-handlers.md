---
type: decision-note
title: Org-scoped command handlers must call OwnershipGuard.EnsureIsOrgMemberAsync
description: Holding the platform-wide organisator role is not enough to act on a specific organization - a handler must also check the requester belongs to that org.
tags: [backend, authorization, organizations]
timestamp: 2026-07-16
---

# Schema

A role check alone (e.g. "does this user have the organisator role anywhere") doesn't prove the requester belongs to the *specific* organization a command targets. Any org-scoped mutation needs an explicit membership check against that organization, not just a role check - in this codebase, `OwnershipGuard.EnsureIsOrgMemberAsync`.

# Examples

`RemoveMemberCommandHandler` called Keycloak directly on the strength of the requester holding the organisator role, with no check that they belonged to the target organization - letting any organizer remove members of any org, not just their own. The fix adds `RequestingUserId` to `RemoveMemberCommand`, populated from the JWT subject claim (matching the existing pattern already used by `UpdateOrganization`), and calls `OwnershipGuard.EnsureIsOrgMemberAsync(keycloakOrganizationService, request.OrganizationId, request.RequestingUserId, cancellationToken)` before `RemoveMemberAsync`.

A related test gotcha surfaced in the same fix: a JWT minted before `CreateOrganizationAsync` doesn't carry the organisator role granted during org creation, so a test must re-authenticate after a role grant to pick up the updated role claim before asserting on it.

# Citations

- commit `edfc574` - fix: enforce org-membership ownership check on RemoveMember endpoint (#581)
- `backend/src/Application/Organizations/RemoveMember/v1/RemoveMemberCommandHandler.cs`
- `backend/src/Application/Common/Authorization/OwnershipGuard.cs`
