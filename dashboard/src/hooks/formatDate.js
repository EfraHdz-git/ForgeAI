const TZ = 'America/Mexico_City';

function parseUtc(dateStr) {
  if (!dateStr) return null;
  // SQL Server returns UTC without Z suffix — append it so JS parses as UTC
  const s = dateStr.endsWith('Z') ? dateStr : dateStr + 'Z';
  return new Date(s);
}

export function formatTime(dateStr) {
  const d = parseUtc(dateStr);
  if (!d) return '';
  return d.toLocaleTimeString('en-US', { timeZone: TZ, hour: '2-digit', minute: '2-digit' });
}

export function formatDate(dateStr) {
  const d = parseUtc(dateStr);
  if (!d) return '';
  return d.toLocaleDateString('en-US', { timeZone: TZ, month: 'short', day: 'numeric' });
}

export function formatDateTime(dateStr) {
  const d = parseUtc(dateStr);
  if (!d) return '';
  return d.toLocaleString('en-US', { timeZone: TZ, month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}
