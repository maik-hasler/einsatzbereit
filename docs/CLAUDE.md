# Docs — Architecture Documentation

## Structure

```
docs/
├── Architektur/
│   ├── Architektur.adoc        Master document (includes all sections below)
│   ├── src/
│   │   ├── 01_introduction_and_goals.adoc
│   │   ├── 02_architecture_constraints.adoc
│   │   ├── 03_context_and_scope.adoc
│   │   ├── 04_solution_strategy.adoc
│   │   ├── 05_building_block_view.adoc
│   │   ├── 06_runtime_view.adoc
│   │   ├── 07_deployment_view.adoc
│   │   ├── 08_concepts.adoc
│   │   ├── 09_architecture_decisions.adoc
│   │   ├── 10_quality_requirements.adoc
│   │   ├── 11_technical_risks.adoc
│   │   ├── 12_glossary.adoc
│   │   └── config.adoc         Locale/language config (German)
│   └── images/
│       ├── business-context.puml   PlantUML diagram
│       └── quality-tree.puml       PlantUML diagram
└── ADRs/
    ├── 1_monorepository.adoc   Accepted 2026-03-23
    ├── 2_arc42.adoc            Accepted 2026-03-25
    └── 3_keycloak.adoc         Accepted 2026-03-25
```

## Format

- **AsciiDoc** (`.adoc`) — not Markdown
- **arc42** template (standard German architecture documentation format)
- Diagrams in **PlantUML** (`.puml`) embedded via `asciidoctor-diagram`
- Built to HTML5 via `asciidoctor-action` in CI, deployed to GitHub Pages

## Build

Triggered by `.github/workflows/docs.yml` on push to `main` (paths: `docs/**`).

Local build requires Asciidoctor + asciidoctor-diagram gem. Easier to push and let CI build.

## ADR Conventions

File naming: `{number}_{kebab-title}.adoc`  
Each ADR documents: context, decision, status (Accepted/Proposed/Deprecated), consequences.

When creating a new ADR:
- Next number in sequence
- Status: `Proposed` initially, `Accepted` once agreed
- Reference in `09_architecture_decisions.adoc` if needed
