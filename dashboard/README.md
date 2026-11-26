# ForgeAI Dashboard

React 19 + Vite front end for ForgeAI. It serves two audiences from one app:

- **Operators (admin accounts):** pipeline overview, niche selection and on-demand runs, opportunity list and detail, evaluation reasoning, approval gates, prototype deployment and the activity log.
- **Users:** submit an idea, watch it move through evaluation and market research in real time, read the generated report and share it or its prototype through revocable links.

The sign-in page also lists read-only demo reports and prototypes for visitors without an account.

## Structure

- `src/pages/` screens (Dashboard, Opportunities, OpportunityDetail, Reviews, Activity, SubmitIdea, Processing, Report, UserDashboard, Login, ChangePassword)
- `src/components/` shared pieces (pipeline explainer, share dialog, demo links)
- `src/context/AuthContext.jsx` JWT session handling
- `src/hooks/useApi.js` API client; uses `http://localhost:5050/api` when running on localhost and `/api` otherwise

Live stage updates come from the API's Server-Sent Events stream (`/api/sse/stream`).

## Development

```bash
npm install
npm run dev     # http://localhost:5173, expects the API on http://localhost:5050
npm run build   # outputs dist/, which the API can serve via Pipeline:DashboardPath
npm run lint
```

The Vite dev server proxies `/preview/*` to the API so generated prototypes open from the dashboard during development.
