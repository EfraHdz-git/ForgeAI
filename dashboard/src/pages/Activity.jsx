import { useState, useEffect } from 'react';
import { fetchJson } from '../hooks/useApi';
import { formatDateTime } from '../hooks/formatDate';

export default function Activity() {
  const [logs, setLogs] = useState([]);
  const [runs, setRuns] = useState([]);
  const [tab, setTab] = useState('logs');

  useEffect(() => {
    fetchJson('/pipeline/logs?count=100').then(setLogs).catch(console.error);
    fetchJson('/pipeline/runs?count=50').then(setRuns).catch(console.error);
  }, []);

  return (
    <div className="activity-page">
      <h2>Activity</h2>

      <div className="filter-bar">
        <button className={tab === 'logs' ? 'active' : ''} onClick={() => setTab('logs')}>Pipeline Logs</button>
        <button className={tab === 'runs' ? 'active' : ''} onClick={() => setTab('runs')}>Agent Runs</button>
      </div>

      {tab === 'logs' && (
        <div className="activity-feed full">
          {logs.map(log => (
            <div key={log.id} className="activity-item">
              <span className="activity-time">{formatDateTime(log.timestamp)}</span>
              <span className={`activity-stage tag-${log.stage}`}>{log.stage}</span>
              <span className="activity-action">{log.action}</span>
              {log.opportunityId && <span className="activity-opp">#{log.opportunityId}</span>}
              {log.details && <span className="activity-details">{log.details}</span>}
            </div>
          ))}
          {logs.length === 0 && <div className="empty">No logs yet</div>}
        </div>
      )}

      {tab === 'runs' && (
        <div className="activity-feed full">
          {runs.map(run => (
            <div key={run.id} className={`activity-item ${run.status === 'failed' ? 'run-failed' : ''}`}>
              <span className="activity-time">{formatDateTime(run.createdAt)}</span>
              <span className={`activity-stage tag-${run.jobType}`}>{run.jobType}</span>
              <span className={`run-status status-${run.status}`}>{run.status}</span>
              {run.completedAt && run.startedAt && (
                <span className="run-duration">
                  {Math.round((new Date(run.completedAt) - new Date(run.startedAt)) / 1000)}s
                </span>
              )}
              {run.error && <span className="activity-details error">{run.error.slice(0, 100)}</span>}
            </div>
          ))}
          {runs.length === 0 && <div className="empty">No agent runs yet</div>}
        </div>
      )}
    </div>
  );
}
