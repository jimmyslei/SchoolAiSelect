---
applyTo:
  - "packages/client/src/**/*.tsx"
  - "packages/client/src/**/*.css"
description: "Use when updating React UI, client layouts, visual polish, component styling, responsive behavior, or HomePage presentation in packages/client."
---

# Client UI Guidelines

- Preserve the existing warm visual language from `packages/client/src/index.css`; extend the current warm, sand, sage, sky, gold, and neutral tokens instead of introducing a new color system.
- Reuse existing primitives and shared patterns before creating new ones, especially `components/ui`, `components/shared`, `surface-panel`, `surface-elevated`, and existing button variants.
- Keep the UI soft and editorial rather than generic SaaS: large rounded shapes, warm gradients, layered surfaces, and restrained motion fit this app better than flat cards or harsh contrast.
- Maintain the current responsive behavior. New layouts should work cleanly on mobile first and then expand to the existing wide-screen composition style.
- When touching search flow screens, preserve the current step-based structure in `pages/HomePage.tsx` instead of introducing parallel flows or duplicating stateful UI.
- Prefer updating existing tokens, utility classes, and shared components over hard-coded ad hoc colors, spacing, or one-off button styles.
- Keep component text and labels aligned with the current product voice: concise, product-facing, and consistent with the existing Chinese UI copy.
