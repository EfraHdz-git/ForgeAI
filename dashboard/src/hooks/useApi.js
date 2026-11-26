const API = window.location.hostname === 'localhost' ? 'http://localhost:5050/api' : '/api';

function getAuthHeaders() {
  const token = localStorage.getItem('forgeai_token');
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return headers;
}

export async function fetchJson(path) {
  const res = await fetch(`${API}${path}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}

export async function postJson(path, body = null) {
  const res = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}

export function subscribeSSE(onEvent) {
  const es = new EventSource(`${API}/sse/stream`);

  es.addEventListener('connected', (e) => {
    console.log('SSE connected', JSON.parse(e.data));
  });

  const events = [
    'scout.completed', 'opportunity.created', 'opportunity.evaluated',
    'opportunity.promoted', 'prototype.started', 'prototype.completed',
    'preview.ready', 'review.completed', 'opportunity.stage',
    'pipeline.started', 'pipeline.scout_done', 'pipeline.completed', 'pipeline.failed'
  ];

  events.forEach((evt) => {
    es.addEventListener(evt, (e) => {
      onEvent(evt, JSON.parse(e.data));
    });
  });

  es.onerror = () => console.warn('SSE connection error, will retry...');

  return () => es.close();
}
