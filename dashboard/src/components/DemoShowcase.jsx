// Read-only demos shown on the sign-in page for visitors without an account (e.g. interviewers).
// Each link is a permanent share token (share_tokens, expires_at NULL, created by user 7); revoke with is_active = 0.
// Only ideas that have the full pipeline output: evaluation, market research and a working prototype.
const DEMOS = [
  {
    name: 'PostSpark',
    score: 71,
    report: '/?share=HhqVffCDEhO-FOVyWPReErJUVJZYep9o',
    prototype: '/preview/212/?share=-A8EVjmLIJYTXaXwASW4nTrx43B9AguK',
  },
  {
    name: 'Sponsor Match',
    score: 66,
    report: '/?share=kbPPj3u-6bUFQCoAILHi-EM1DYgJocEn',
    prototype: '/preview/304/?share=8cJhOolSqxMNMGEci8MoKlMfixB4rai9',
  },
];

export default function DemoShowcase() {
  return (
    <section className="demo-showcase" aria-labelledby="demo-title">
      <h3 id="demo-title">No account? Explore live demos</h3>
      <p className="demo-intro">Ideas that went through the full pipeline: evaluation, market research and the prototype it generated.</p>
      <ul>
        {DEMOS.map((d) => (
          <li key={d.name}>
            <span className="demo-name">{d.name}</span>
            <span className="demo-score">{d.score}/100</span>
            <a href={d.report} target="_blank" rel="noopener noreferrer">Report</a>
            <a href={d.prototype} target="_blank" rel="noopener noreferrer">Prototype</a>
          </li>
        ))}
      </ul>
    </section>
  );
}
