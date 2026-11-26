import { useState, useEffect } from 'react';
import { postJson, fetchJson } from '../hooks/useApi';

export default function ShareModal({ opportunityId, type = 'report', hasPrototype = false, onClose }) {
  const [url, setUrl] = useState(null);
  const [loading, setLoading] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState(null);
  const [existingShares, setExistingShares] = useState([]);
  const [mode, setMode] = useState(type); // report | prototype

  useEffect(() => {
    fetchJson(`/user/ideas/${opportunityId}/shares`)
      .then(setExistingShares)
      .catch(() => {});
  }, [opportunityId]);

  const generate = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await postJson(`/user/ideas/${opportunityId}/share`, { type: mode });
      setUrl(res.url);
      const refreshed = await fetchJson(`/user/ideas/${opportunityId}/shares`);
      setExistingShares(refreshed);
    } catch {
      setError('Failed to create share link.');
    }
    setLoading(false);
  };

  const copy = () => {
    if (!url) return;
    navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const revoke = async (shareId) => {
    try {
      await fetch(`${window.location.hostname === 'localhost' ? 'http://localhost:5050/api' : '/api'}/user/shares/${shareId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${localStorage.getItem('forgeai_token')}` },
      });
      setExistingShares(prev => prev.filter(s => s.id !== shareId));
    } catch {}
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal share-modal" onClick={e => e.stopPropagation()}>
        <div className="share-modal-header">
          <h3>Share this {mode}</h3>
          <button className="modal-close" onClick={onClose}>&times;</button>
        </div>

        {hasPrototype && (
          <div className="share-tabs">
            <button className={mode === 'report' ? 'active' : ''} onClick={() => { setMode('report'); setUrl(null); }}>Report</button>
            <button className={mode === 'prototype' ? 'active' : ''} onClick={() => { setMode('prototype'); setUrl(null); }}>Prototype</button>
          </div>
        )}

        <p className="share-modal-desc">
          {mode === 'report'
            ? 'Generate a link to share this report. Anyone with the link can view it read-only.'
            : 'Generate a link to share your prototype. Anyone with the link can access the live preview.'}
        </p>

        {!url ? (
          <button className="btn-brand share-generate-btn" onClick={generate} disabled={loading}>
            {loading ? 'Generating...' : 'Generate Share Link'}
          </button>
        ) : (
          <div className="share-link-row">
            <input type="text" value={url} readOnly onFocus={e => e.target.select()} />
            <button className="btn-brand" onClick={copy}>{copied ? 'Copied!' : 'Copy'}</button>
          </div>
        )}

        {error && <p className="share-error">{error}</p>}

        <p className="share-modal-note">Links expire in 7 days. You can revoke access any time.</p>

        {existingShares.filter(s => s.type === mode).length > 0 && (
          <div className="share-existing">
            <div className="share-existing-label">Active links</div>
            {existingShares.filter(s => s.type === mode).map(s => (
              <div key={s.id} className="share-existing-row">
                <span className="share-existing-token">...{s.token.slice(-8)}</span>
                <span className="share-existing-views">{s.view_count} views</span>
                <button className="share-revoke" onClick={() => revoke(s.id)}>Revoke</button>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
