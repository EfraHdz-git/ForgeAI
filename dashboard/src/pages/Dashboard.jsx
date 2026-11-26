import { useState, useEffect, useRef } from 'react';
import { fetchJson, postJson, subscribeSSE } from '../hooks/useApi';

const STAGES = [
  { key: 'pendingEvaluation', status: 'pending_evaluation', label: 'Pending Eval', icon: '?' },
  { key: 'pendingApproval', status: 'pending_approval', label: 'Approve to Build', icon: '!' },
  { key: 'prototypeQueued', status: 'prototype_queued', label: 'Build Queued', icon: '...' },
  { key: 'prototyping', status: 'prototyping', label: 'Building', icon: '' },
  { key: 'pendingReview', status: 'pending_review', label: 'Review', icon: '' },
  { key: 'approved', status: 'approved_for_mvp', label: 'MVP', icon: '' },
];

function getOpportunityType(summary) {
  if (!summary) return null;
  const match = summary.match(/^\[(painkiller|vitamin|trend|unknown)\]/i);
  return match ? match[1].toLowerCase() : null;
}

function NicheMultiSelect({ profiles, selected, onChange }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    const close = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  const toggle = (id) => {
    onChange(selected.includes(id) ? selected.filter(s => s !== id) : [...selected, id]);
  };

  const selectAll = () => {
    onChange(selected.length === profiles.length ? [] : profiles.map(p => p.id));
  };

  const label = selected.length === 0
    ? 'Select niches...'
    : selected.length === profiles.length
      ? 'All niches'
      : profiles.filter(p => selected.includes(p.id)).map(p => p.display_name).join(', ');

  return (
    <div className="niche-select" ref={ref}>
      <button className="niche-select-trigger" onClick={() => setOpen(!open)}>
        <span className="niche-select-label">{label}</span>
        <svg width="12" height="12" viewBox="0 0 12 12" fill="none" style={{ transform: open ? 'rotate(180deg)' : '' }}>
          <path d="M3 5l3 3 3-3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
        </svg>
      </button>
      {open && (
        <div className="niche-dropdown">
          <label className="niche-option niche-option-all" onClick={selectAll}>
            <input type="checkbox" checked={selected.length === profiles.length} readOnly />
            <span>Select all</span>
          </label>
          <div className="niche-divider" />
          {profiles.map(p => (
            <label key={p.id} className="niche-option" onClick={() => toggle(p.id)}>
              <input type="checkbox" checked={selected.includes(p.id)} readOnly />
              <span>{p.display_name}</span>
            </label>
          ))}
        </div>
      )}
    </div>
  );
}

export default function Dashboard({ refreshTick, onViewOpportunity }) {
  const [opportunities, setOpportunities] = useState([]);
  const [loading, setLoading] = useState(true);
  const [profiles, setProfiles] = useState([]);
  const [selectedProfiles, setSelectedProfiles] = useState([]);
  const [running, setRunning] = useState(false);
  const [pipelineStatus, setPipelineStatus] = useState(null); // null | { stage, detail }

  useEffect(() => {
    fetchJson('/dashboard/opportunities')
      .then(setOpportunities)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [refreshTick]);

  useEffect(() => {
    fetchJson('/dashboard/focus-profiles')
      .then(p => {
        setProfiles(p);
        setSelectedProfiles(p.map(x => x.id));
      })
      .catch(console.error);

    // Check if pipeline is already running (survives refresh)
    fetchJson('/dashboard/pipeline/status')
      .then(s => {
        if (s.running) {
          setRunning(true);
          setPipelineStatus({ stage: 'scouting', detail: 'Pipeline is running...' });
        }
      })
      .catch(console.error);
  }, []);

  // Listen for pipeline SSE events
  useEffect(() => {
    const unsub = subscribeSSE((type, data) => {
      if (type === 'pipeline.started') {
        setRunning(true);
        setPipelineStatus({ stage: 'scouting', detail: 'Fetching signals from sources...' });
      } else if (type === 'pipeline.scout_done') {
        setPipelineStatus({
          stage: 'evaluating',
          detail: `Scout done: ${data?.Signals || data?.signals || 0} signals, ${data?.Opportunities || data?.opportunities || 0} new opportunities. Now evaluating...`
        });
      } else if (type === 'pipeline.completed') {
        setRunning(false);
        setPipelineStatus({
          stage: 'done',
          detail: `Pipeline complete: ${data?.Evaluated || data?.evaluated || 0} evaluated, ${data?.Opportunities || data?.opportunities || 0} new opportunities.`
        });
        setTimeout(() => setPipelineStatus(null), 15000);
      } else if (type === 'pipeline.failed') {
        setRunning(false);
        setPipelineStatus({ stage: 'failed', detail: data?.Error || data?.error || 'Pipeline failed.' });
        setTimeout(() => setPipelineStatus(null), 15000);
      }
    });
    return unsub;
  }, []);

  const runPipeline = async () => {
    if (selectedProfiles.length === 0 || running) return;
    setRunning(true);
    setPipelineStatus({ stage: 'starting', detail: 'Starting pipeline...' });
    try {
      await postJson('/dashboard/pipeline/run', { profileIds: selectedProfiles });
      setPipelineStatus({ stage: 'scouting', detail: 'Fetching signals from sources...' });
    } catch (err) {
      console.error(err);
      setRunning(false);
      setPipelineStatus({ stage: 'failed', detail: 'Failed to start pipeline.' });
      setTimeout(() => setPipelineStatus(null), 5000);
    }
  };

  if (loading) return <div className="loading">Loading pipeline...</div>;

  const stageData = STAGES.map(stage => ({
    ...stage,
    items: opportunities.filter(o => {
      if (stage.key === 'approved') return o.status === 'approved' || o.status === 'approved_for_mvp';
      return o.status === stage.status;
    }),
  }));

  const totalActive = opportunities.filter(o => o.status !== 'rejected' && o.status !== 'dismissed').length;

  return (
    <div className="dashboard">
      <div className="pipeline-title-row">
        <h2>Pipeline</h2>
        <span className="pipeline-total">{totalActive} active</span>
      </div>

      {/* Run Pipeline controls */}
      <div className="pipeline-controls">
        <NicheMultiSelect profiles={profiles} selected={selectedProfiles} onChange={setSelectedProfiles} />
        <button
          className="btn-brand btn-run-pipeline"
          onClick={runPipeline}
          disabled={running || selectedProfiles.length === 0}
        >
          {running ? 'Running...' : 'Run Pipeline'}
        </button>
      </div>

      {/* Pipeline status banner */}
      {pipelineStatus && (
        <div className={`pipeline-status pipeline-status-${pipelineStatus.stage}`}>
          {(pipelineStatus.stage === 'starting' || pipelineStatus.stage === 'scouting' || pipelineStatus.stage === 'evaluating') && (
            <div className="pipeline-status-spinner" />
          )}
          {pipelineStatus.stage === 'done' && <span className="pipeline-status-icon">&#10003;</span>}
          {pipelineStatus.stage === 'failed' && <span className="pipeline-status-icon">&#10007;</span>}
          <span className="pipeline-status-text">{pipelineStatus.detail}</span>
          {pipelineStatus.stage === 'done' && (
            <button className="pipeline-status-dismiss" onClick={() => setPipelineStatus(null)}>&times;</button>
          )}
        </div>
      )}

      {/* Roadmap track */}
      <div className="roadmap">
        <div className="roadmap-track" />
        <div className="roadmap-steps">
          {stageData.map((stage, i) => {
            const hasItems = stage.items.length > 0;
            return (
              <div key={stage.key} className={`roadmap-step ${hasItems ? 'has-items' : ''}`}>
                <div className="roadmap-node">
                  <span className="roadmap-dot">{stage.items.length}</span>
                  <span className="roadmap-label">{stage.label}</span>
                </div>
                {hasItems && (
                  <div className="roadmap-cards">
                    {stage.items.map(opp => {
                      const type = getOpportunityType(opp.evaluation_summary);
                      const displayTitle = opp.plain_title || opp.title;
                      return (
                        <div key={opp.id} className="pipeline-card" onClick={() => onViewOpportunity(opp.id)}>
                          <div className="card-title">{displayTitle}</div>
                          <div className="card-meta">
                            {type && <span className={`type-badge type-${type}`}>{type}</span>}
                            {opp.user_type && <span className="card-user">{opp.user_type}</span>}
                            {opp.deploy_url && <span className="card-badge preview">Preview</span>}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
