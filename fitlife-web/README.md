# FitLife Web

Vue 3 and TypeScript single-page application for browsing gym classes, managing
member preferences, and viewing explainable personalized recommendations.

## Stack

- Vue 3
- TypeScript
- Vite
- Pinia
- Vue Router
- Axios
- Tailwind CSS
- Vitest

## Local development

```bash
npm ci --legacy-peer-deps
npm run dev
```

The frontend uses `VITE_API_URL` when provided and otherwise sends requests to
`/api`.

## Validate

```bash
npm run lint
npm test
npm run build
npm audit --omit=dev --audit-level=high
```

The production build includes TypeScript validation. The repository-level
PowerShell verification script runs both backend and frontend checks:

```powershell
../scripts/verify.ps1
```

## Demo limitation

The current portfolio demo stores its JWT in browser `localStorage`. Use only
synthetic demo accounts and data. A real-user deployment requires a separate
session-security decision.
