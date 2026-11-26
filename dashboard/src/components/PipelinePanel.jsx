// Recruiter-facing explanation of the ForgeAI pipeline, shown on the sign-in page.
// Every claim is verified against the code and production runs (2026-11-25): keep it that way when editing.
const STATS = [
  { value: '4', label: 'AI agents' },
  { value: '2', label: 'signal sources' },
  { value: '~1 min', label: 'idea to report' },
  { value: '~2 min', label: 'prototype build' },
];

const STEPS = [
  {
    title: 'Signal scouting',
    text: 'A Scout agent collects launches and discussions from Product Hunt and Hacker News. Claude extracts the underlying problems and clusters related signals into opportunities.',
    tags: ['Product Hunt API', 'Hacker News', 'Amazon Bedrock'],
  },
  {
    title: 'Skeptical evaluation',
    text: 'An evaluator agent prompted as a skeptical investor scores market fit, competition, monetization, complexity and clarity as structured JSON. Below 40 is rejected, 40 to 69 watched, 70+ goes to prototyping, and a readiness check guards vague ideas.',
    tags: ['Claude on Bedrock', 'Structured output', 'Scoring rubric'],
  },
  {
    title: 'Market intelligence',
    text: 'A Claude Code research agent runs live web searches for real competitors, user complaints, gaps, pricing and risks, writes a market report, and the idea is re-scored with that evidence.',
    tags: ['Claude Code', 'Web research', 'Re-evaluation'],
  },
  {
    title: 'Spec and prototype',
    text: 'Claude writes a product spec, then Claude Code builds a working React app headlessly. The build must pass before it is deployed to a private preview behind revocable share links.',
    tags: ['Claude Code CLI', 'React', 'Vite', 'Automated build gate'],
  },
  {
    title: 'Human review loop',
    text: 'The only human step: review what was built and approve it, reject it, or request a rebuild. Every decision is recorded with the opportunity.',
    tags: ['Human approval', 'SQL Server', 'Dapper'],
  },
  {
    title: 'Real-time delivery',
    text: 'Server-Sent Events stream every stage to the dashboard live, and reports, digests and sign-in links go out by email.',
    tags: ['SSE', 'Amazon SES', 'JWT auth', '.NET 9'],
  },
];

export default function PipelinePanel() {
  return (
    <section className="pipeline-panel" aria-labelledby="pipeline-title">
      <p className="pipeline-eyebrow">Multi-agent product discovery</p>
      <h2 id="pipeline-title">From market signals to a working prototype</h2>
      <p className="pipeline-intro">
        A thin orchestrator coordinates specialised agents: they find problems, evaluate like an investor,
        research competitors and build the app. A person only decides what is worth keeping.
      </p>
      <dl className="pipeline-stats">
        {STATS.map((s) => (
          <div key={s.label}>
            <dt>{s.value}</dt>
            <dd>{s.label}</dd>
          </div>
        ))}
      </dl>
      <ol className="pipeline-steps">
        {STEPS.map((step, i) => (
          <li key={step.title}>
            <span className="pipeline-num">{i + 1}</span>
            <div>
              <h3>{step.title}</h3>
              <p>{step.text}</p>
              <div className="pipeline-tags">
                {step.tags.map((t) => <span key={t}>{t}</span>)}
              </div>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
