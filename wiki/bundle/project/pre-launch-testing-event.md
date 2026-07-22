---
type: plan
title: Pre-launch live testing event
description: A moderated role-card testing session with human guests before 1.0 - the human counterpart to the automated persona-simulation run.
tags: [launch, testing, personas, event, 1.0, oldenburg, persona, a11y]
timestamp: 2026-07-18
---

# Pre-launch live testing event

A live testing session to run before publishing 1.0. Guests (friends and
colleagues) each get a role card - a persona with a specific use case or
perspective and a goal to work through. If more people show up than there are
distinct personas, duplicating a persona under a different name is fine.

# Role cards

Candidate personas, each testing from a different angle:

- a hacker trying to break the site
- an organizer with many needs
- an organizer with few needs
- a volunteer with interest
- someone unfamiliar with technology
- a platform admin moderating organizations, users, and other things

# The human half of persona testing

This event is the human counterpart to the automated persona-simulation routine,
which drives the same kinds of personas (Organizer Olaf, Volunteer Vera) against
live staging. The two channels catch different things. Live guests exercise
product judgment and react to friction a script cannot feel, and the moderator
can fix a blocker bug on the spot. The automated run covers the same personas
unattended and repeatedly, but only files issues - it never writes code. The
"someone unfamiliar with technology" card is worth pairing with the frontend
accessibility patterns that ESLint's jsx-a11y ruleset cannot catch, so its
findings map onto a review gap that is already known.

# Logistics

- Timing: end of August to mid-September 2026.
- Venue: ask the author's company for a room for the evening.
- Guests: friends and colleagues, each with a role card.
- Food/drinks: pizza and drinks, a relaxed evening alongside the testing.
- Author's role: a short intro on the vision and the goal of the evening, then
  float and support anyone stuck, fix critical blocker bugs live, and write up
  issues for anything less critical (bugs, feedback, feature ideas).

# Open items

- Make a printable PDF of the personas, one role card per persona.

# Related
- [autonomous-routines](/decisions/autonomous-routines.md) - persona-simulation automates, against the same personas, what this event does with human guests
- [frontend-conventions](/reference/frontend-conventions.md) - the low-tech-user card exercises the a11y patterns jsx-a11y cannot verify
- [project-vision](/project/project-vision.md) - the vision the opening intro covers, and the ship-for-Oldenburg-first priority this event serves

# Citations
- wiki/notes/1-pre-launch-live-testing-event.md
