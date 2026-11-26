import React, { useState, useEffect } from 'react';
import { fetchJson, postJson, subscribeSSE } from '../hooks/useApi';
import { useAuth } from '../context/AuthContext';

function getType(summary) {
  if (!summary) return null;
  const m = summary.match(/^\[(painkiller|vitamin|trend|unknown)\]/i);
  return m ? m[1].toLowerCase() : null;
}

function getVerdict(score) {
  if (score == null) return null;
  if (score >= 70) return { label: 'Strong', cls: 'strong' };
  if (score >= 50) return { label: 'Medium', cls: 'medium' };
  return { label: 'Weak', cls: 'weak' };
}

function timeAgo(d) {
  if (!d) return '';
  const now = new Date();
  const then = new Date(d);
  const diff = Math.floor((now - then) / 1000);
  if (diff < 60) return 'Just now';
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
  if (diff < 604800) return `${Math.floor(diff / 86400)}d ago`;
  return then.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

const COLUMNS = [
  { key: 'submitted', label: 'Idea' },
  { key: 'evaluated', label: 'Score' },
  { key: 'researched', label: 'Market' },
  { key: 'report', label: 'Report' },
  { key: 'prototype', label: 'Prototype' },
  { key: 'business', label: 'Plan' },
  { key: 'resubmit', label: 'Improve' },
];

async function openPreview(opportunityId) {
  try {
    const res = await postJson(`/user/ideas/${opportunityId}/share`, { type: 'prototype' });
    // Open via path so Vite proxy (local) or nginx (prod) serves through /preview
    const path = `/preview/${opportunityId}/?share=${res.token}`;
    window.open(path, '_blank');
  } catch (err) {
    console.error(err);
    alert('Failed to open preview. Please try again.');
  }
}

function IdeaRow({ item, onViewReport }) {
  const idea = item.idea;
  const market = item.market;
  const title = idea.plain_title || idea.title;
  const type = getType(idea.evaluation_summary);
  const verdict = getVerdict(idea.overall_score);
  const hasEval = idea.overall_score != null;
  const hasResearch = market != null;
  const hasReport = hasEval && idea.status !== 'pending_evaluation';
  const hasPrototype = !!idea.deploy_url;
  const isProcessing = idea.status === 'pending_evaluation';
  const isBuilding = ['prototype_queued', 'prototyping'].includes(idea.status) || ['queued', 'generating', 'building'].includes(idea.prototype_status);

  return (
    <div className="timeline-row">
      {/* Idea */}
      <div className="timeline-cell timeline-cell-filled">
        <div className="timeline-cell-title">{title}</div>
        <div className="timeline-cell-meta-line">
          {type && <span className={`type-badge type-${type}`}>{type}</span>}
          {verdict && <><span className="timeline-cell-dot">·</span><span className={`verdict-text-${verdict.cls}`}>{verdict.label}</span></>}
          <span className="timeline-cell-dot">·</span>
          <span className="timeline-cell-date">{timeAgo(idea.created_at)}</span>
        </div>
      </div>
      <div className="timeline-connector filled" />

      {/* Score */}
      <div className={`timeline-cell ${hasEval ? 'timeline-cell-filled' : isProcessing ? 'timeline-cell-active' : ''}`}>
        {hasEval ? (
          <>
            <div className={`timeline-cell-score score-${verdict?.cls}`}>{idea.overall_score}</div>
            <div className={`timeline-cell-verdict-label verdict-text-${verdict?.cls}`}>{verdict?.label} opportunity</div>
          </>
        ) : isProcessing ? (
          <div className="timeline-cell-spinner"><div className="mini-spinner" /> Analyzing</div>
        ) : null}
      </div>
      <div className={`timeline-connector ${hasEval ? 'filled' : ''}`} />

      {/* Market */}
      <div className={`timeline-cell ${hasResearch ? 'timeline-cell-filled' : hasEval && !hasResearch && !isProcessing ? 'timeline-cell-active' : ''}`}>
        {hasResearch ? (
          <>
            <div className="timeline-cell-stat">{market.competitor_count} competitors</div>
            <div className={`timeline-cell-indicator indicator-${market.competition_level}`}>{(market.competition_level || '').charAt(0).toUpperCase() + (market.competition_level || '').slice(1)} competition</div>
            <div className="timeline-cell-sub-muted">{(market.market_maturity || '').charAt(0).toUpperCase() + (market.market_maturity || '').slice(1)} market</div>
          </>
        ) : hasEval && !hasResearch && idea.status !== 'rejected' ? (
          <div className="timeline-cell-spinner"><div className="mini-spinner" /> Searching</div>
        ) : null}
      </div>
      <div className={`timeline-connector ${hasResearch ? 'filled' : ''}`} />

      {/* Report */}
      <div className={`timeline-cell ${hasReport ? 'timeline-cell-filled' : ''}`}>
        {hasReport ? (
          <button className="btn-brand btn-brand-sm timeline-action-primary" onClick={() => onViewReport(idea.id)}>
            View Report
          </button>
        ) : null}
      </div>
      <div className={`timeline-connector ${hasPrototype ? 'filled' : ''}`} />

      {/* Prototype */}
      <div className={`timeline-cell ${hasPrototype ? 'timeline-cell-filled' : isBuilding ? 'timeline-cell-active' : hasReport ? 'timeline-cell-cta' : ''}`}>
        {hasPrototype ? (
          <button className="btn-brand btn-brand-sm timeline-action" onClick={() => openPreview(idea.id)}>
            Open Preview
          </button>
        ) : isBuilding ? (
          <div className="timeline-cell-spinner"><div className="mini-spinner" /> Building</div>
        ) : hasReport ? (
          <div className="timeline-sell-wrap" title="Upgrade to generate a working app prototype">
            <button className="timeline-action-sell" onClick={() => alert('Coming soon — we\'ll generate a working app from this idea')}>
              <svg width="12" height="12" viewBox="0 0 16 16" fill="none"><rect x="3" y="7" width="10" height="7" rx="1" stroke="currentColor" strokeWidth="1.5"/><path d="M5 7V5a3 3 0 016 0v2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
              Build Your App
            </button>
            <span className="timeline-sell-hint">Generate a working app from this idea</span>
          </div>
        ) : null}
      </div>
      <div className={`timeline-connector ${hasPrototype ? 'filled' : ''}`} />

      {/* Plan */}
      <div className={`timeline-cell ${hasReport ? 'timeline-cell-cta' : ''}`}>
        {hasReport ? (
          <button className="timeline-action-sell" onClick={() => alert('Coming soon — generate a full business plan from your report')}>
            <svg width="14" height="14" viewBox="0 0 16 16" fill="none"><path d="M3 3h10v10H3z" stroke="currentColor" strokeWidth="1.5"/><path d="M6 6h4M6 9h2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
            Generate Plan
          </button>
        ) : null}
      </div>
      <div className="timeline-connector" />

      {/* Improve */}
      <div className={`timeline-cell ${hasReport ? 'timeline-cell-cta' : ''}`}>
        {hasReport ? (
          <button className="timeline-action-sell" onClick={() => alert('Coming soon — refine your idea with new context and re-run the analysis')}>
            <svg width="14" height="14" viewBox="0 0 16 16" fill="none"><path d="M2 12l3-3 3 3 5-5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
            Improve & Rerun
          </button>
        ) : null}
      </div>
    </div>
  );
}

export default function UserDashboard({ onSubmit, onViewReport }) {
  const { user } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const load = () => fetchJson('/user/ideas').then(setItems).catch(console.error);
    load().finally(() => setLoading(false));

    // Auto-refresh on relevant SSE events
    const unsub = subscribeSSE((type) => {
      if (['opportunity.evaluated', 'opportunity.stage', 'prototype.completed', 'preview.ready'].includes(type)) {
        load();
      }
    });
    return unsub;
  }, []);

  if (loading) return <div className="loading">Loading your ideas...</div>;

  const reportCount = items.filter(i => i.idea.overall_score != null && i.idea.status !== 'pending_evaluation').length;
  const protoCount = items.filter(i => i.idea.deploy_url).length;

  if (items.length === 0) {
    return (
      <div className="user-dashboard">
        <div className="user-dashboard-empty">
          <div className="user-dashboard-icon">&#9889;</div>
          <h2>Welcome to ForgeAI</h2>
          <p>You have no ideas yet. Submit your first idea and get a real market analysis in minutes.</p>
          <button className="btn-brand" onClick={onSubmit}>Submit Your First Idea</button>
        </div>
      </div>
    );
  }

  return (
    <div className="user-dashboard">
      <div className="user-dashboard-header">
        <div>
          <h2>Your Ideas</h2>
          <p className="user-dashboard-stats">
            {items.length} {items.length === 1 ? 'idea' : 'ideas'} · {reportCount} {reportCount === 1 ? 'report' : 'reports'} · {protoCount} {protoCount === 1 ? 'prototype' : 'prototypes'}
          </p>
        </div>
        <div className="user-dashboard-actions">
          <div className="usage-badge">
            <span className="usage-count">{user?.reportsUsed ?? 0} of {user?.tier === 'pro' ? '10' : '1'}</span>
            <span className="usage-label">reports used</span>
            {user?.tier !== 'pro' && <button className="usage-upgrade" onClick={() => alert('Coming soon — unlock prototypes, plans & more reports for $29/mo')}>Unlock Pro</button>}
          </div>
          <button className="btn-brand" onClick={onSubmit}>New Idea</button>
        </div>
      </div>

      <div className="timeline-board">
        {/* Column headers */}
        <div className="timeline-header-row">
          {COLUMNS.map((col, i) => (
            <React.Fragment key={col.key}>
              {i > 0 && <div />}
              <div className="timeline-header">{col.label}</div>
            </React.Fragment>
          ))}
        </div>

        {/* Idea rows */}
        {items.map(item => {
          const hasReport = item.idea.overall_score != null && item.idea.status !== 'pending_evaluation';
          const hasPrototype = !!item.idea.deploy_url;
          const hint = !hasReport ? null
            : hasPrototype ? 'Share your prototype or generate a business plan'
            : 'Start by reviewing your report to decide your next step';
          return (
            <React.Fragment key={item.idea.id}>
              <IdeaRow item={item} onViewReport={onViewReport} />
              {hint && <div className="timeline-row-hint">{hint}</div>}
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
}
