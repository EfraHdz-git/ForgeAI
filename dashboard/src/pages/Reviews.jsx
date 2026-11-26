import { useState, useEffect } from 'react';
import { fetchJson } from '../hooks/useApi';

function getOpportunityType(summary) {
  if (!summary) return null;
  const match = summary.match(/^\[(painkiller|vitamin|trend|unknown)\]/i);
  return match ? match[1].toLowerCase() : null;
}

const TABS = [
  { key: 'all', label: 'All' },
  { key: 'gate1', label: 'Approve to Build' },
  { key: 'gate2', label: 'Review Prototype' },
];

export default function Reviews({ onSelect }) {
  const [opportunities, setOpportunities] = useState([]);
  const [tab, setTab] = useState('all');

  useEffect(() => {
    Promise.all([
      fetchJson('/dashboard/opportunities?status=pending_approval'),
      fetchJson('/dashboard/opportunities?status=pending_review'),
      fetchJson('/dashboard/opportunities?status=prototype_built'),
      fetchJson('/dashboard/opportunities?status=revision_requested'),
    ]).then(results => {
      setOpportunities(results.flat().sort((a, b) => (b.overall_score || 0) - (a.overall_score || 0)));
    }).catch(console.error);
  }, []);

  const gate1 = opportunities.filter(o => o.status === 'pending_approval');
  const gate2 = opportunities.filter(o => o.status !== 'pending_approval');

  const visible = tab === 'gate1' ? gate1 : tab === 'gate2' ? gate2 : opportunities;

  const countFor = (key) => key === 'gate1' ? gate1.length : key === 'gate2' ? gate2.length : opportunities.length;

  return (
    <div className="reviews-page">
      <div className="page-title-row">
        <h2>Reviews</h2>
        <span className="page-title-count">{opportunities.length} pending</span>
      </div>

      {opportunities.length === 0 ? (
        <div className="empty-state">
          <p>Nothing needs your attention right now.</p>
        </div>
      ) : (
        <>
          <div className="filter-bar">
            {TABS.map(t => (
              <button
                key={t.key}
                className={`${tab === t.key ? 'active' : ''} ${countFor(t.key) === 0 ? 'empty-filter' : ''}`}
                onClick={() => setTab(t.key)}
              >
                {t.label}
                <span className="filter-count">{countFor(t.key)}</span>
              </button>
            ))}
          </div>

          <div className="review-cards">
            {visible.map(opp => <ReviewCard key={opp.id} opp={opp} onSelect={onSelect} />)}
          </div>

          {visible.length === 0 && <div className="empty-state"><p>No items in this category.</p></div>}
        </>
      )}
    </div>
  );
}

function ReviewCard({ opp, onSelect }) {
  const type = getOpportunityType(opp.evaluation_summary);
  const displayTitle = opp.plain_title || opp.title;

  return (
    <div className="review-card" onClick={() => onSelect(opp.id)}>
      <div className="review-card-header">
        <h3>{displayTitle}</h3>
        {opp.overall_score != null && (
          <span className={`score-badge ${opp.overall_score >= 70 ? 'high' : opp.overall_score >= 40 ? 'mid' : 'low'}`}>
            {opp.overall_score}
          </span>
        )}
      </div>

      {opp.simple_use_case ? (
        <p className="review-use-case">{opp.simple_use_case}</p>
      ) : (
        <p className="review-problem">{opp.problem_statement?.slice(0, 150)}{opp.problem_statement?.length > 150 ? '...' : ''}</p>
      )}

      <div className="review-meta">
        <span className={`status-badge status-${opp.status?.replace(/_/g, '-')}`}>{opp.status?.replace(/_/g, ' ')}</span>
        {type && <span className={`type-badge type-${type}`}>{type}</span>}
        {opp.deploy_url && (
          <a href={opp.deploy_url} target="_blank" rel="noreferrer" className="btn-small" onClick={e => e.stopPropagation()}>
            Preview
          </a>
        )}
      </div>
    </div>
  );
}
