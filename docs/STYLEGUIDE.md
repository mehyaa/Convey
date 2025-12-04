---
layout: default
title: STYLEGUIDE
parent: Home
---
# Documentation Style Guide

Consistent documentation improves discoverability and lowers onboarding friction. Follow these guidelines when updating or adding docs in this repository.

## Principles
- Clarity over completeness: prefer concise examples to exhaustive API listings.
- Action first: start pages with what the reader can DO (Quick Start) before deep theory.
- Explicit defaults: state when conventions apply (e.g. snake_case queues, UTC timestamps).
- Single responsibility: one concept per markdown file.

## Structure
1. H1: Package or Guide Title
2. Short one‑line value statement / summary
3. Quick Start or minimal usage snippet
4. Core Concepts / Options (sub‑headings `##` or `###`)
5. Advanced / Integration notes
6. Troubleshooting / FAQ (optional)
7. Links & next steps

## Formatting
- Use sentence case for headings (except proper nouns).
- Prefer fenced code blocks with language identifiers: `csharp`, `pwsh`, `bash`, `json`, `yaml`.
- Keep lines <= 120 chars when practical.
- Use backticks for inline identifiers: `ICommandHandler`, `rabbitmq.exchange`.
- Lists: use `-` not `*` for bullets; avoid trailing punctuation unless full sentences.

## Code Samples
- Show minimal compilable snippets (omit unrelated boilerplate).
- Prefer modern .NET minimal hosting patterns.
- Include missing `using` directives if non-obvious.
- Avoid artificial regions (`#region`).

## Terminology
| Term | Preferred Usage |
|------|-----------------|
| microservice | microservice (lowercase) |
| CQRS | CQRS (uppercase acronym) |
| JWT | JWT (uppercase) |
| MongoDB | MongoDB (camel case) |
| Redis | Redis |
| RabbitMQ | RabbitMQ |
| Jaeger | Jaeger |
| Vault | Vault |
| mTLS | mTLS (lower m, upper TLS) |

## Style Notes
- Avoid marketing adjectives ("revolutionary", "ultimate").
- Use active voice: "Register handlers" not "Handlers are registered".
- Prefer future-proof wording: avoid hardcoding versions unless necessary.
- Link external tech on first mention only.

## Samples Directory References
When referencing sample services use inline code formatting: `Conveyor.Services.Orders`.

## Security & Secrets
Do not include real secrets. Use placeholders like `your-super-secret-key`.

## Validation Phrases
Use: "Ensure X is configured" rather than "Make sure X" or "Be sure to".

## Checklist Before Commit
- Headings consistent
- Spelling / grammar pass
- External links verified
- Code fences have language
- No sensitive data or credentials

---
For questions or improvements open an issue referencing `STYLEGUIDE.md`.

