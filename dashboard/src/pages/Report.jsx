import { useState, useEffect, useRef } from 'react';
import { fetchJson, postJson } from '../hooks/useApi';
import ShareModal from '../components/ShareModal';

function getType(summary) {
  if (!summary) return null;
  const m = summary.match(/^\[(painkiller|vitamin|trend|unknown)\]/i);
  return m ? m[1].toLowerCase() : null;
}

function verdictFromScore(score) {
  if (score == null) return { label: 'Not evaluated', tone: 'neutral', text: 'This idea has not been scored yet.' };
  if (score >= 70) return { label: 'Strong', tone: 'strong', text: 'High-potential opportunity worth prototyping.' };
  if (score >= 50) return { label: 'Medium', tone: 'medium', text: 'Has potential but needs refinement before building.' };
  return { label: 'Weak', tone: 'weak', text: 'Significant challenges — consider refining or dropping.' };
}

function getDomain(url) {
  if (!url || url === 'unknown') return null;
  try {
    const u = new URL(url.startsWith('http') ? url : `https://${url}`);
    return u.hostname.replace(/^www\./, '');
  } catch {
    return null;
  }
}

function AnimatedNumber({ value, duration = 1200 }) {
  const [display, setDisplay] = useState(0);
  useEffect(() => {
    if (value == null) return;
    let start = null;
    const from = 0;
    const to = value;
    const step = (ts) => {
      if (!start) start = ts;
      const progress = Math.min((ts - start) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      setDisplay(Math.round(from + (to - from) * eased));
      if (progress < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }, [value, duration]);
  return display;
}

function useReveal(trigger) {
  const ref = useRef(null);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const items = el.querySelectorAll('.reveal:not(.revealed)');
    if (items.length === 0) return;
    const io = new IntersectionObserver((entries) => {
      entries.forEach((e) => {
        if (e.isIntersecting) {
          e.target.classList.add('revealed');
          io.unobserve(e.target);
        }
      });
    }, { threshold: 0.05, rootMargin: '0px 0px -20px 0px' });
    items.forEach((i) => io.observe(i));
    return () => io.disconnect();
  }, [trigger]);
  return ref;
}

function CompetitorCard({ c, index }) {
  const [imgError, setImgError] = useState(false);
  const domain = getDomain(c.url);
  const shotUrl = domain ? `https://image.thum.io/get/width/600/crop/400/noanimate/${encodeURIComponent(`https://${domain}`)}` : null;
  const faviconUrl = domain ? `https://www.google.com/s2/favicons?domain=${domain}&sz=64` : null;

  const clickable = c.url && c.url !== 'unknown';
  const Tag = clickable ? 'a' : 'div';
  const props = clickable ? { href: c.url, target: '_blank', rel: 'noreferrer' } : {};

  return (
    <Tag {...props} className="competitor-card reveal" style={{ animationDelay: `${index * 80}ms` }}>
      <div className="competitor-thumb">
        {shotUrl && !imgError ? (
          <img src={shotUrl} alt={c.name} onError={() => setImgError(true)} loading="lazy" />
        ) : (
          <div className="competitor-thumb-fallback">
            {faviconUrl ? <img src={faviconUrl} alt="" /> : <span>{c.name?.[0]?.toUpperCase() || '?'}</span>}
          </div>
        )}
        {faviconUrl && !imgError && (
          <div className="competitor-favicon">
            <img src={faviconUrl} alt="" />
          </div>
        )}
      </div>
      <div className="competitor-info">
        <div className="competitor-name">{c.name}</div>
        {c.description && <div className="competitor-desc">{c.description}</div>}
        <div className="competitor-meta">
          {c.type && <span className="competitor-tag">{c.type}</span>}
          {c.pricing && <span className="competitor-pricing">{c.pricing}</span>}
        </div>
        {domain && <div className="competitor-domain">{domain}</div>}
      </div>
    </Tag>
  );
}

function getUniquenessLabel(market) {
  if (market?.uniqueness_level) {
    const map = { high_novelty: 'High Novelty', moderate: 'Moderate', low: 'Low' };
    return map[market.uniqueness_level] || market.uniqueness_level;
  }
  if (market?.competition_level) {
    const map = { none: 'High Novelty', low: 'High Novelty', medium: 'Moderate', high: 'Low' };
    return map[market.competition_level] || 'Moderate';
  }
  return null;
}

function getOneLiner(opp, eval_) {
  if (opp.simple_use_case) return opp.simple_use_case;
  if (eval_?.summary) {
    const stripped = eval_.summary.replace(/^\[(painkiller|vitamin|trend|unknown)\]\s*/i, '');
    return stripped.split('.')[0] + '.';
  }
  return null;
}

export default function Report({ id, onBack, onRefresh, readOnly = false, dataOverride = null, shareToken = null }) {
  const [data, setData] = useState(dataOverride);
  const [loading, setLoading] = useState(false);
  const [scrollProgress, setScrollProgress] = useState(0);
  const [showShare, setShowShare] = useState(false);
  const containerRef = useReveal(data);

  useEffect(() => {
    if (dataOverride) { setData(dataOverride); return; }
    // Use user-scoped endpoint when not in readOnly (shared) mode
    const path = readOnly && shareToken
      ? `/public/report/${shareToken}`
      : `/user/ideas/${id}/report`;
    fetchJson(path).then(setData).catch(console.error);
  }, [id, readOnly, shareToken, dataOverride]);

  useEffect(() => {
    const onScroll = () => {
      const h = document.documentElement;
      const total = h.scrollHeight - h.clientHeight;
      setScrollProgress(total > 0 ? (h.scrollTop / total) * 100 : 0);
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  if (!data) return <div className="loading">Loading report...</div>;

  const { opportunity: opp, evaluation: eval_, prototype: proto, marketReport: market } = data;
  const type = getType(eval_?.summary);
  const verdict = verdictFromScore(eval_?.overall_score);
  const displayTitle = opp.plain_title || opp.title;

  const doAction = async (action) => {
    setLoading(true);
    try {
      if (action === 'promote') {
        await postJson(`/dashboard/opportunities/${id}/promote`, { reason: 'Approved from report' });
      }
      const refreshed = await fetchJson(`/user/ideas/${id}/report`);
      setData(refreshed);
      if (onRefresh) onRefresh();
    } catch (err) { console.error(err); }
    setLoading(false);
  };

  const oneLiner = getOneLiner(opp, eval_);
  const uniqueness = getUniquenessLabel(market);
  const nextSteps = market?.next_steps || (market?.gap_opportunities?.length > 0 ? market.gap_opportunities : null);
  const risks = market?.biggest_risks || (market?.brutal_honesty ? [market.brutal_honesty] : null);
  const marketTiming = market?.market_timing || opp.why_now;
  const canBuild = ['pending_approval', 'watching', 'rejected'].includes(opp.status);

  return (
    <div className="report-page" ref={containerRef}>
      {/* Scroll progress */}
      <div className="report-progress-bar" style={{ transform: `scaleX(${scrollProgress / 100})` }} />

      {/* 1. Hero — name + one-liner */}
      <div className="report-hero">
        <div className="report-hero-bg" />
        {!readOnly && (
          <button className="report-back-inline" onClick={onBack} aria-label="Back">
            <svg width="14" height="14" viewBox="0 0 16 16" fill="none"><path d="M10 12L6 8l4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
          </button>
        )}
        {!readOnly && (
          <button className="report-share-btn" onClick={() => setShowShare(true)}>
            <svg width="14" height="14" viewBox="0 0 16 16" fill="none"><path d="M8 2v10M8 2l-3 3M8 2l3 3M2 11v3h12v-3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
            Share
          </button>
        )}
        <div className="report-hero-content">
          <div className="report-hero-top">
            <span className="report-hero-badge">Venture Report</span>
            {type && <span className={`type-badge type-${type}`}>{type}</span>}
            {uniqueness && <span className={`uniqueness-badge uniqueness-${uniqueness.toLowerCase().replace(/\s+/g, '-')}`}>{uniqueness}</span>}
          </div>
          <h1 className="report-title">{displayTitle}</h1>
          {oneLiner && (
            <p className="report-use-case">{oneLiner}</p>
          )}
          <div className="report-hero-meta">
            {opp.user_type && <span className="report-hero-meta-item"><strong>For</strong> {opp.user_type}</span>}
            {opp.target_audience && opp.target_audience !== opp.user_type && (
              <span className="report-hero-meta-item"><strong>Target</strong> {opp.target_audience}</span>
            )}
          </div>
        </div>
      </div>

      {/* Category Block — only shows when the product creates/defines a category */}
      {market?.category_name && (
        <section className="report-category reveal">
          <div className="report-category-inner">
            <div className="report-category-col">
              <span className="report-category-label">Category</span>
              <h3 className="report-category-name">{market.category_name}</h3>
            </div>
            {market.category_position && (
              <div className="report-category-col">
                <span className="report-category-label">Position</span>
                <p className="report-category-value">{market.category_position}</p>
              </div>
            )}
            {market.category_why_now && (
              <div className="report-category-col">
                <span className="report-category-label">Why now</span>
                <p className="report-category-value">{market.category_why_now}</p>
              </div>
            )}
          </div>
        </section>
      )}

      {/* 2. Verdict + score + legend + inline CTA */}
      <section className={`report-verdict reveal verdict-${verdict.tone}`}>
        <div className="verdict-left">
          <span className="report-label">Verdict</span>
          <h2 className="verdict-label">{verdict.label}</h2>
          <p className="verdict-text">{verdict.text}</p>
          {market?.recommendation && (
            <p className="verdict-quote">"{market.recommendation}"</p>
          )}
          <div className="score-legend">
            <span className="score-legend-item legend-strong">70+ Strong</span>
            <span className="score-legend-item legend-medium">50-70 Needs work</span>
            <span className="score-legend-item legend-weak">&lt;50 Weak</span>
          </div>
        </div>
        {eval_?.overall_score != null && (
          <div className="verdict-score">
            <div className="verdict-score-ring">
              <svg viewBox="0 0 120 120" className="score-ring-svg">
                <circle cx="60" cy="60" r="52" className="score-ring-bg" />
                <circle cx="60" cy="60" r="52" className="score-ring-fill"
                  style={{ strokeDashoffset: 326.7 - (326.7 * eval_.overall_score) / 100 }} />
              </svg>
              <div className="verdict-score-text">
                <span className="verdict-score-num"><AnimatedNumber value={eval_.overall_score} /></span>
                <span className="verdict-score-suffix">/100</span>
              </div>
            </div>
          </div>
        )}
      </section>

      {/* Inline CTA — right after verdict */}
      {canBuild && !readOnly && (
        <div className="report-inline-cta reveal">
          <div className="inline-cta-text">
            <strong>Ready to see this idea come to life?</strong>
            <span>We'll generate a working prototype from this report.</span>
          </div>
          <button className="btn-brand inline-cta-btn" onClick={() => doAction('promote')} disabled={loading}>
            {loading ? 'Processing...' : 'Build a Working Prototype'}
          </button>
        </div>
      )}

      {/* Market research pending */}
      {!market && (
        <div className="report-pending-banner reveal">
          <div className="report-pending-icon">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="7" stroke="currentColor" strokeWidth="1.5"/><path d="M10 6v5M10 14h.01" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
          </div>
          <div>
            <strong>Market research not yet run.</strong>
            <p>Competitor analysis, next steps, and revenue projections will appear here once the research step completes.</p>
          </div>
        </div>
      )}

      {/* 3. Next Steps — the decision driver */}
      {nextSteps && nextSteps.length > 0 && (
        <section className="report-section report-next-steps reveal">
          <span className="report-label">What You Should Do Next</span>
          <ol className="next-steps-list">
            {nextSteps.map((s, i) => <li key={i}>{s}</li>)}
          </ol>
        </section>
      )}

      {/* 4. Problem + Market Timing side by side */}
      <div className="report-row-2col reveal">
        <section className="report-section report-section-editorial">
          <span className="report-label">The Problem</span>
          <p className="report-body report-body-lead">{opp.problem_statement}</p>
        </section>
        {marketTiming && (
          <section className="report-section report-section-why-now">
            <span className="report-label">Market Timing</span>
            <p className="report-body">{marketTiming}</p>
          </section>
        )}
      </div>

      {/* Evaluator analysis (fallback when no market data) */}
      {!market && eval_?.reasoning && (
        <section className="report-section reveal">
          <span className="report-label">Evaluator Analysis</span>
          <p className="report-body report-reasoning">{eval_.reasoning}</p>
          {(eval_.market_fit_score != null || eval_.monetization_score != null) && (
            <div className="score-breakdown">
              {eval_.market_fit_score != null && <ScoreBar label="Market Fit" value={eval_.market_fit_score} />}
              {eval_.competition_score != null && <ScoreBar label="Competition" value={eval_.competition_score} />}
              {eval_.monetization_score != null && <ScoreBar label="Monetization" value={eval_.monetization_score} />}
              {eval_.complexity_score != null && <ScoreBar label="Complexity" value={eval_.complexity_score} />}
            </div>
          )}
        </section>
      )}

      {/* 5. Competitors */}
      {market && market.competitors && market.competitors.length > 0 && (
        <section className="report-section-wide reveal">
          <div className="section-header-row">
            <div>
              <span className="report-label">Market Reality</span>
              <h2 className="section-headline">Who's already in this space</h2>
            </div>
            <div className="market-stats-inline">
              <div className="market-stat-inline">
                <span className="market-stat-inline-value">{market.competitor_count_found}</span>
                <span className="market-stat-inline-label">competitors</span>
              </div>
              <div className="market-stat-divider" />
              <div className="market-stat-inline">
                <span className="market-stat-inline-value capitalize">{market.competition_level}</span>
                <span className="market-stat-inline-label">competition</span>
              </div>
              <div className="market-stat-divider" />
              <div className="market-stat-inline">
                <span className="market-stat-inline-value capitalize">{market.market_maturity}</span>
                <span className="market-stat-inline-label">maturity</span>
              </div>
            </div>
          </div>
          <div className="competitor-grid">
            {market.competitors.map((c, i) => <CompetitorCard key={i} c={c} index={i} />)}
          </div>
        </section>
      )}

      {/* 6. Pain + Gain side by side */}
      {(market?.common_complaints?.length > 0 || market?.gap_opportunities?.length > 0) && (
        <div className="report-row-2col reveal">
          {market?.common_complaints?.length > 0 && (
            <section className="report-section report-section-pain">
              <span className="report-label">The Pain</span>
              <h3 className="section-subheadline">What users complain about</h3>
              <ul className="insight-list insight-list-pain">
                {market.common_complaints.map((c, i) => <li key={i}>{c}</li>)}
              </ul>
            </section>
          )}
          {market?.gap_opportunities?.length > 0 && (
            <section className="report-section report-section-gain">
              <span className="report-label">The Opening</span>
              <h3 className="section-subheadline">Where you could win</h3>
              <ul className="insight-list insight-list-gain">
                {market.gap_opportunities.map((g, i) => <li key={i}>{g}</li>)}
              </ul>
            </section>
          )}
        </div>
      )}

      {/* 7. Dominant Player */}
      {market?.dominant_player?.name && (
        <section className="report-section-wide report-dominant reveal">
          <span className="report-label">Who Dominates</span>
          <h2 className="dominant-name">{market.dominant_player.name}</h2>
          <div className="dominant-grid">
            <div className="dominant-box dominant-win">
              <div className="dominant-box-label">Why they win</div>
              <p>{market.dominant_player.why_they_win}</p>
            </div>
            <div className="dominant-box dominant-fail">
              <div className="dominant-box-label">Where they fail</div>
              <p>{market.dominant_player.why_they_fail}</p>
            </div>
          </div>
        </section>
      )}

      {/* 8. Risks — trust builder */}
      {risks && risks.length > 0 && (
        <section className="report-section report-risks reveal">
          <span className="report-label">Biggest Risks</span>
          <h3 className="section-subheadline">If this fails, it will be because...</h3>
          <ul className="insight-list insight-list-risk">
            {risks.map((r, i) => <li key={i}>{r}</li>)}
          </ul>
        </section>
      )}

      {/* Revenue Projection */}
      {market?.revenue_projection && (
        <section className="report-section-wide report-revenue-section reveal">
          <div className="section-header-row">
            <div>
              <span className="report-label">Revenue Potential</span>
              <h2 className="section-headline">The money picture</h2>
            </div>
          </div>
          {market.revenue_projection.pricing_benchmark && (
            <p className="revenue-benchmark">{market.revenue_projection.pricing_benchmark}</p>
          )}
          <div className="revenue-grid">
            {market.revenue_projection.early_users_estimate && (
              <div className="revenue-card">
                <div className="revenue-stage">Early Stage</div>
                <div className="revenue-value">{market.revenue_projection.early_users_estimate}</div>
              </div>
            )}
            {market.revenue_projection.scaled_users_estimate && (
              <div className="revenue-card revenue-card-highlight">
                <div className="revenue-stage">At Scale</div>
                <div className="revenue-value">{market.revenue_projection.scaled_users_estimate}</div>
              </div>
            )}
          </div>
          {market.revenue_projection.notes && (
            <p className="revenue-notes">{market.revenue_projection.notes}</p>
          )}
        </section>
      )}

      {/* Prototype preview */}
      {proto?.deploy_url && (
        <section className="report-section report-preview-section reveal">
          <span className="report-label">Working Prototype</span>
          <a href={proto.deploy_url} target="_blank" rel="noreferrer" className="btn-brand prototype-cta">
            Open Live Preview
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" style={{ marginLeft: 8 }}><path d="M6 4l4 4-4 4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
          </a>
        </section>
      )}

      {/* 9. Bottom CTA — the conversion point */}
      {!readOnly && (
        <div className="report-actions reveal">
          {canBuild && (
            <button className="btn-brand report-cta-primary" onClick={() => doAction('promote')} disabled={loading}>
              {loading ? 'Processing...' : 'Build a Working Prototype'}
            </button>
          )}
          {opp.status === 'pending_review' && proto?.deploy_url && (
            <a href={proto.deploy_url} target="_blank" rel="noreferrer" className="btn-brand report-cta-primary">
              View Live Prototype
            </a>
          )}
          <button className="report-cta-secondary" disabled title="Coming soon">
            Generate Business Plan
          </button>
          <button className="report-cta-secondary" disabled title="Coming soon">
            Request Revision
          </button>
        </div>
      )}

      {readOnly && (
        <div className="report-shared-footer">
          <div className="report-shared-footer-brand">
            <span className="nav-icon">&#9889;</span>
            <span>Generated by <strong>ForgeAI</strong></span>
          </div>
          <a href="/" className="report-shared-footer-cta">Validate your own idea →</a>
        </div>
      )}

      {showShare && (
        <ShareModal
          opportunityId={id}
          type="report"
          hasPrototype={!!proto?.deploy_url}
          onClose={() => setShowShare(false)}
        />
      )}
    </div>
  );
}

function ScoreBar({ label, value }) {
  const [width, setWidth] = useState(0);
  useEffect(() => {
    const t = setTimeout(() => setWidth(value), 200);
    return () => clearTimeout(t);
  }, [value]);
  return (
    <div className="score-breakdown-row">
      <span className="score-breakdown-label">{label}</span>
      <div className="score-breakdown-track">
        <div className="score-breakdown-fill" style={{ width: `${width}%` }} />
      </div>
      <span className="score-breakdown-value">{value}</span>
    </div>
  );
}
