import { useState, useEffect } from 'react';
import { fetchJson } from '../hooks/useApi';
import { formatDate } from '../hooks/formatDate';

const STATUS_LABELS = {
  pending_evaluation: 'Pending Eval',
  watching: 'Watching',
  rejected: 'Rejected',
  pending_approval: 'Needs Approval',
  prototype_queued: 'Proto Queued',
  prototyping: 'Building',
  prototype_built: 'Proto Built',
  prototype_failed: 'Proto Failed',
  deployed: 'Deployed',
  pending_review: 'Review',
  revision_requested: 'Revision',
  approved: 'Approved',
  approved_for_mvp: 'MVP',
  dismissed: 'Dismissed',
};

const FILTERS = ['all', 'pending_approval', 'watching', 'pending_evaluation', 'prototype_queued', 'pending_review', 'approved', 'rejected', 'dismissed'];

function getOpportunityType(summary) {
  if (!summary) return null;
  const match = summary.match(/^\[(painkiller|vitamin|trend|unknown)\]/i);
  return match ? match[1].toLowerCase() : null;
}

export default function Opportunities({ onSelect }) {
  const [allOpportunities, setAllOpportunities] = useState([]);
  const [filter, setFilter] = useState('all');

  useEffect(() => {
    fetchJson('/dashboard/opportunities').then(setAllOpportunities).catch(console.error);
  }, []);

  const filtered = (filter === 'all'
    ? allOpportunities
    : allOpportunities.filter(o => o.status === filter || (filter === 'approved' && o.status === 'approved_for_mvp'))
  ).sort((a, b) => (b.overall_score || 0) - (a.overall_score || 0));

  const countFor = (f) => {
    if (f === 'all') return allOpportunities.length;
    if (f === 'approved') return allOpportunities.filter(o => o.status === 'approved' || o.status === 'approved_for_mvp').length;
    return allOpportunities.filter(o => o.status === f).length;
  };

  return (
    <div className="opportunities-page">
      <div className="page-title-row">
        <h2>Opportunities</h2>
        <span className="page-title-count">{allOpportunities.length} total</span>
      </div>

      <div className="filter-bar">
        {FILTERS.map(f => {
          const count = countFor(f);
          return (
            <button key={f} className={`${filter === f ? 'active' : ''} ${count === 0 ? 'empty-filter' : ''}`} onClick={() => setFilter(f)}>
              {f === 'all' ? 'All' : STATUS_LABELS[f] || f}
              <span className="filter-count">{count}</span>
            </button>
          );
        })}
      </div>

      <div className="opp-table">
        <div className="opp-header">
          <span className="col-title">Opportunity</span>
          <span className="col-score">Score</span>
          <span className="col-status">Status</span>
          <span className="col-type">Type</span>
          <span className="col-date">Created</span>
          <span className="col-actions">Actions</span>
        </div>
        {filtered.map(opp => {
          const type = getOpportunityType(opp.evaluation_summary);
          const displayTitle = opp.plain_title || opp.title;

          return (
            <div key={opp.id} className="opp-row" onClick={() => onSelect(opp.id)}>
              <span className="col-title">{displayTitle}</span>
              <span className="col-score">
                {opp.overall_score != null && (
                  <span className={`score-pill ${opp.overall_score >= 70 ? 'high' : opp.overall_score >= 40 ? 'mid' : 'low'}`}>
                    {opp.overall_score}
                  </span>
                )}
              </span>
              <span className="col-status">
                <span className={`status-badge status-${opp.status?.replace(/_/g, '-')}`}>
                  {STATUS_LABELS[opp.status] || opp.status}
                </span>
              </span>
              <span className="col-type">
                {type ? <span className={`type-badge type-${type}`}>{type}</span> : '-'}
              </span>
              <span className="col-date">{formatDate(opp.created_at)}</span>
              <span className="col-actions">
                {opp.deploy_url && (
                  <a href={`${opp.deploy_url}`} target="_blank" rel="noreferrer" className="btn-small" onClick={e => e.stopPropagation()}>
                    Preview
                  </a>
                )}
              </span>
            </div>
          );
        })}
        {filtered.length === 0 && <div className="empty">No opportunities found</div>}
      </div>
    </div>
  );
}
