import { useState, useEffect } from 'react';
import { fetchJson, postJson } from '../hooks/useApi';
import { formatDateTime } from '../hooks/formatDate';

export default function OpportunityDetail({ id, onBack, onRefresh, onToast, onViewReport, backLabel }) {
  const [data, setData] = useState(null);
  const [actionNote, setActionNote] = useState('');
  const [loading, setLoading] = useState(false);
  const [showRejectModal, setShowRejectModal] = useState(false);

  const load = () => fetchJson(`/dashboard/opportunities/${id}`).then(setData).catch(console.error);

  useEffect(() => { load(); }, [id]);

  if (!data) return <div className="loading">Loading...</div>;

  const { opportunity: opp, evaluation: eval_, prototype: proto, timeline } = data;
  const displayTitle = opp.plain_title || opp.title;

  const doAction = async (action) => {
    setLoading(true);
    try {
      if (action === 'promote') {
        await postJson(`/dashboard/opportunities/${id}/promote`, { reason: actionNote || 'Manual override' });
        onToast?.(`"${displayTitle}" approved for prototype`, 'success');
      } else if (action === 'dismiss') {
        await postJson(`/dashboard/opportunities/${id}/dismiss`, { reason: actionNote || 'Dismissed' });
        onToast?.(`"${displayTitle}" rejected`, 'info');
      } else if (action === 'approve') {
        await postJson(`/dashboard/opportunities/${id}/promote`, { reason: actionNote || 'Approved for MVP' });
        onToast?.(`"${displayTitle}" approved for MVP`, 'success');
      } else if (action === 'reopen') {
        await postJson(`/dashboard/opportunities/${id}/reopen`);
        onToast?.(`"${displayTitle}" reopened`, 'success');
      } else if (action === 'deploy') {
        await postJson(`/prototype/deploy/${id}`);
        onToast?.('Deployment started', 'success');
      } else if (action === 'revise') {
        await postJson(`/dashboard/opportunities/${id}/promote`, { reason: actionNote });
        onToast?.(`New version requested for "${displayTitle}"`, 'success');
      }
      await load();
      setActionNote('');
      onRefresh();
    } catch (err) {
      console.error(err);
      onToast?.('Action failed. Please try again.', 'error');
    }
    setLoading(false);
  };

  const type = eval_?.summary?.match(/^\[(painkiller|vitamin|trend|unknown)\]/i)?.[1]?.toLowerCase();

  const hasActions = ['pending_approval', 'watching', 'rejected', 'pending_review', 'dismissed'].includes(opp.status);

  const versionLabel = proto?.version_number > 1 ? `v${proto.version_number + 1}` : 'v2';

  return (
    <div className="detail-page">
      {/* Header row */}
      <div className="detail-top">
        <button className="btn-back" onClick={onBack}>
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M10 12L6 8l4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
        </button>
        <h2 className="detail-title">{displayTitle}</h2>
        <span className={`status-badge status-${opp.status?.replace(/_/g, '-')}`}>{opp.status?.replace(/_/g, ' ')}</span>
        {type && <span className={`type-badge type-${type}`}>{type}</span>}
        {onViewReport && eval_ && (
          <button className="btn-brand btn-brand-sm" onClick={() => onViewReport(id)} style={{ marginLeft: 'auto' }}>View Report</button>
        )}
        {proto?.deploy_url && (
          <a href={proto.deploy_url} target="_blank" rel="noreferrer" className="btn-brand btn-brand-sm" style={{ marginLeft: onViewReport && eval_ ? '0' : 'auto' }}>Open Preview</a>
        )}
        {proto && proto.build_ready && !proto.deploy_url && (
          <button className="btn-brand btn-brand-sm" onClick={() => doAction('deploy')} disabled={loading} style={{ marginLeft: onViewReport && eval_ ? '0' : 'auto' }}>Deploy</button>
        )}
      </div>
      {opp.title !== displayTitle && <p className="detail-product-name">{opp.title}</p>}

      {/* Two-column body */}
      <div className="detail-layout">
        <div className="detail-main">
          {eval_ && (
            <div className="detail-section detail-section-grow">
              <h3>Evaluation</h3>
              <div className="detail-score-card">
                <span className={`detail-score ${eval_.overall_score >= 70 ? 'high' : eval_.overall_score >= 40 ? 'mid' : 'low'}`}>
                  {eval_.overall_score}
                </span>
                <span className="detail-score-label">/100</span>
                {eval_.clarity_score != null && (
                  <div className="detail-clarity-pill">Clarity {eval_.clarity_score}</div>
                )}
              </div>
              <p className="eval-reasoning">{eval_.reasoning}</p>
            </div>
          )}

          <div className="detail-section">
            <h3>Problem</h3>
            <p>{opp.problem_statement || 'N/A'}</p>

            {opp.simple_use_case && (
              <>
                <h3>Use Case</h3>
                <p className="use-case-text">{opp.simple_use_case}</p>
              </>
            )}

            {opp.user_type && (
              <div className="detail-meta-row">
                {opp.user_type && <span className="detail-chip">User: {opp.user_type}</span>}
                {opp.moment_of_pain && <span className="detail-chip">When: {opp.moment_of_pain}</span>}
                {opp.bad_workaround && <span className="detail-chip">Workaround: {opp.bad_workaround}</span>}
              </div>
            )}
          </div>

          {proto && !proto.build_ready && proto.status === 'failed' && (
            <div className="detail-section">
              <h3>Prototype</h3>
              <span className="detail-chip error">Build failed: {proto.error_message}</span>
            </div>
          )}
        </div>

        {/* Right column: timeline */}
        <div className="detail-sidebar">
          {timeline && timeline.length > 0 && (
            <div className="detail-section detail-timeline-section">
              <h3>Timeline</h3>
              <div className="timeline-full">
                {timeline.map(t => (
                  <div key={t.id} className="timeline-entry">
                    <div className="timeline-dot"></div>
                    <div className="timeline-content">
                      <div className="timeline-top">
                        <span className={`timeline-stage tag-${t.stage}`}>{t.stage}</span>
                        <span className="timeline-action-text">{t.action.replace(/_/g, ' ')}</span>
                      </div>
                      <span className="timeline-date">{formatDateTime(t.timestamp)}</span>
                      {t.details && <p className="timeline-detail-text">{t.details}</p>}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Reject confirmation modal */}
      {showRejectModal && (
        <div className="modal-overlay" onClick={() => setShowRejectModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>Reject this opportunity?</h3>
            <p className="modal-detail">"{displayTitle}" will be dismissed from the pipeline. You can reopen it later if needed.</p>
            <div className="modal-actions">
              <button className="btn-action-reject modal-btn-confirm" onClick={() => { setShowRejectModal(false); doAction('dismiss'); }} disabled={loading}>
                Reject
              </button>
              <button className="modal-btn-cancel" onClick={() => setShowRejectModal(false)}>Cancel</button>
            </div>
          </div>
        </div>
      )}

      {/* Footer — always at bottom */}
      {hasActions && (
        <div className="detail-footer">
          <div className="detail-footer-actions">
            {opp.status === 'pending_approval' && (
              <>
                <button className="btn-action-approve" onClick={() => doAction('promote')} disabled={loading}>Approve for Prototype</button>
                <button className="btn-action-reject" onClick={() => setShowRejectModal(true)} disabled={loading}>Reject</button>
              </>
            )}
            {(opp.status === 'watching' || opp.status === 'rejected') && (
              <button className="btn-promote" onClick={() => doAction('promote')} disabled={loading}>Move to Prototype</button>
            )}
            {opp.status === 'pending_review' && (
              <>
                <button className="btn-action-approve" onClick={() => doAction('approve')} disabled={loading}>Approve for MVP</button>
                <button className="btn-action-reject" onClick={() => setShowRejectModal(true)} disabled={loading}>Reject</button>
              </>
            )}
            {opp.status === 'dismissed' && (
              <button className="btn-reopen" onClick={() => doAction('reopen')} disabled={loading}>Reopen</button>
            )}
          </div>
          {opp.status === 'pending_review' && (
            <div className="detail-footer-revision">
              <div className="revision-top">
                <textarea
                  placeholder="What should be different in the next version..."
                  value={actionNote}
                  onChange={e => setActionNote(e.target.value)}
                  className="revision-textarea"
                  rows={2}
                />
                <button className="btn-brand btn-revision" onClick={() => doAction('revise')} disabled={loading || !actionNote.trim()}>
                  Request New Version
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
