---
type: vision
title: Why Einsatzbereit exists
description: The project's motivation - concrete local volunteering for the Oldenburg area - and how that goal sets the priorities the rest of the bundle runs on.
tags: [vision, motivation, oldenburg, priorities, showcase, learning, deploy-verify, autonomous]
timestamp: 2026-07-18
---

# Why Einsatzbereit exists

The stated purpose is to strengthen volunteering in the Oldenburg area in a
concrete way. Not another large, anonymous platform, but a tool that lets local
associations and organizations show where hands are needed right now, and that
lowers the first hurdle for people who want to help. There is a personal reason
underneath it too: the author has little time to volunteer directly, so the
contribution runs through building the infrastructure that makes it easier for
others, rather than through volunteer hours.

It started partly as a showcase project. That has since been deprioritized:
shipping for the Oldenburg region is now the top priority, ahead of the showcase
angle. Two things in how this repo is run follow directly from that priority,
and reading them as consequences of the vision explains why they are as strict
as they are.

# Ship-for-Oldenburg-first is why verification is non-negotiable

A fix nobody in Oldenburg can actually use is not shipped. That is the reason
behind the deploy-and-verify discipline: every change is observed working on
live staging before the task is called done. The same priority is why polish and
showcase work gets parked the moment it competes with shipping 1.0 - the
deferred CI-gate optimization is the standing example (see the owner boundaries
in autonomous-routines).

# The learning-sandbox motivation is the tooling, not an aside

The project doubles as a place to get better at the craft, currently focused on
the LLM space because that experience carries into the author's day job. That
goal is not abstract. The self-review apparatus and the autonomous routines in
this repo are that motivation made concrete - an unsupervised agent shipping
real work toward a real deadline. Clean code and solid architecture matter here
for the same reason: the point is to learn something, not only to ship
something.

# Related
- [deploy-verify-flow](/process/deploy-verify-flow.md) - ship-for-Oldenburg-first is what makes "not done until observed live" a hard rule
- [autonomous-routines](/decisions/autonomous-routines.md) - the LLM-learning-sandbox motivation turned into working process
- [pre-launch-testing-event](/project/pre-launch-testing-event.md) - a concrete step toward the 1.0 launch this priority drives

# Citations
- wiki/notes/2-why-i-built-this.md
