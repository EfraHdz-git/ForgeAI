import { useState, useEffect } from 'react';
import { fetchJson, subscribeSSE } from '../hooks/useApi';

const STAGES = [
  { key: 'submitted', label: 'Idea received', icon: '1' },
  { key: 'evaluating', label: 'Evaluating your idea', icon: '2' },
  { key: 'researching_market', label: 'Researching competitors', icon: '3' },
  { key: 'finalizing', label: 'Finalizing report', icon: '4' },
  { key: 'report_ready', label: 'Report ready', icon: '5' },
];

const STAGE_INDEX = {};
STAGES.forEach((s, i) => { STAGE_INDEX[s.key] = i; });

const HINTS = {
  submitted: ['Setting things up...'],
  evaluating: [
    'Identifying real user pain...',
    'Checking if this solves a clear need...',
    'Scoring market fit and complexity...',
  ],
  researching_market: [
    'Searching for real competitors...',
    'Analyzing pricing and positioning...',
    'Finding user complaints and gaps...',
    'Scanning product reviews and feedback...',
  ],
  finalizing: [
    'Combining evaluation with market data...',
    'Writing your recommendations...',
    'Almost there...',
  ],
};

export default function Processing({ id, onReport, onBack }) {
  const [currentStage, setCurrentStage] = useState('submitted');
  const [failed, setFailed] = useState(null);
  const [elapsed, setElapsed] = useState(0);
  const [hintIndex, setHintIndex] = useState(0);

  // Timer
  useEffect(() => {
    const t = setInterval(() => setElapsed(e => e + 1), 1000);
    return () => clearInterval(t);
  }, []);

  // Rotate hints every 4 seconds
  useEffect(() => {
    const t = setInterval(() => setHintIndex(i => i + 1), 4000);
    return () => clearInterval(t);
  }, []);

  // SSE listener
  useEffect(() => {
    const unsub = subscribeSSE((type, data) => {
      if (data?.opportunityId !== id && data?.OpportunityId !== id) return;

      if (type === 'opportunity.stage') {
        const stage = data.stage || data.Stage;
        if (stage && STAGE_INDEX[stage] !== undefined) {
          setCurrentStage(stage);
          setHintIndex(0);
        }
        if (stage === 'report_ready') {
          setTimeout(() => onReport(id), 1500);
        }
        if (stage === 'failed') {
          setFailed(data.error || data.Error || 'Something went wrong. Please try again.');
        }
      }

      if (type === 'opportunity.evaluated') {
        setCurrentStage(prev => STAGE_INDEX[prev] < STAGE_INDEX['evaluating'] ? 'evaluating' : prev);
      }
    });
    return unsub;
  }, [id, onReport]);

  // Polling fallback — if SSE events were missed (fired before page connected)
  useEffect(() => {
    const DONE_STATUSES = ['pending_approval', 'watching', 'rejected', 'pending_review', 'prototype_queued', 'prototyping', 'deployed'];
    const poll = setInterval(async () => {
      try {
        const data = await fetchJson(`/dashboard/opportunities/${id}/report`);
        const status = data?.opportunity?.status;
        if (status && DONE_STATUSES.includes(status)) {
          clearInterval(poll);
          setCurrentStage('report_ready');
          setTimeout(() => onReport(id), 1500);
        }
      } catch { /* ignore */ }
    }, 10000);
    return () => clearInterval(poll);
  }, [id, onReport]);

  const currentIdx = STAGE_INDEX[currentStage] ?? 0;
  const mins = Math.floor(elapsed / 60);
  const secs = elapsed % 60;
  const hints = HINTS[currentStage] || [];
  const currentHint = hints.length > 0 ? hints[hintIndex % hints.length] : '';

  return (
    <div className="processing-page">
      <div className="processing-card">
        <div className="processing-header">
          <div className="processing-spinner-ring">
            {currentStage !== 'report_ready' && !failed && <div className="spinner-ring" />}
            {currentStage === 'report_ready' && <div className="processing-check">&#10003;</div>}
            {failed && <div className="processing-fail">!</div>}
          </div>
          <h2>{failed ? 'Analysis failed' : currentStage === 'report_ready' ? 'Your report is ready' : 'Generating your report'}</h2>
          {!failed && currentStage !== 'report_ready' && (
            <p className="processing-timer">{mins}:{secs.toString().padStart(2, '0')}</p>
          )}
        </div>

        <div className="processing-stages">
          {STAGES.map((stage, i) => {
            const done = i < currentIdx || currentStage === 'report_ready';
            const active = i === currentIdx && currentStage !== 'report_ready' && !failed;
            return (
              <div key={stage.key} className={`processing-stage ${done ? 'done' : ''} ${active ? 'active' : ''}`}>
                <div className="stage-indicator">
                  {done ? (
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M3 8l3.5 3.5L13 5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
                  ) : (
                    <span>{stage.icon}</span>
                  )}
                </div>
                <div className="stage-label">{stage.label}</div>
                {active && <div className="stage-dots"><span /><span /><span /></div>}
              </div>
            );
          })}
        </div>

        {/* Dynamic hint */}
        {!failed && currentStage !== 'report_ready' && currentHint && (
          <p className="processing-dynamic-hint" key={currentHint}>{currentHint}</p>
        )}

        {!failed && currentStage !== 'report_ready' && (
          <div className="processing-footer">
            <p className="processing-confidence">We analyze real competitors, pricing, and user complaints — not guesses.</p>
            <p className="processing-save-note">Your report will be saved automatically. You can close this tab and come back.</p>
          </div>
        )}

        {failed && (
          <div className="processing-error">
            <p>{failed}</p>
            <button className="btn-brand" onClick={onBack}>Try Again</button>
          </div>
        )}
      </div>
    </div>
  );
}
