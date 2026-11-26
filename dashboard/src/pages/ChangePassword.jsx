import { useState } from 'react';
import { postJson } from '../hooks/useApi';
import { passwordChecks, isPasswordValid } from '../utils/password';

export default function ChangePassword({ onBack }) {
  const [current, setCurrent] = useState('');
  const [newPw, setNewPw] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);

  const checks = passwordChecks(newPw);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!current || !isPasswordValid(newPw)) return;
    setLoading(true);
    setError(null);
    try {
      await postJson('/auth/change-password', { currentPassword: current, newPassword: newPw });
      setSuccess(true);
    } catch {
      setError('Current password is incorrect.');
    }
    setLoading(false);
  };

  if (success) {
    return (
      <div className="login-page">
        <div className="login-card">
          <div className="login-icon">&#10003;</div>
          <h2>Password updated</h2>
          <p className="login-subtitle">Your password has been changed successfully.</p>
          <button className="btn-brand login-btn" onClick={onBack}>Back to Dashboard</button>
        </div>
      </div>
    );
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <h2>Change Password</h2>
        <form onSubmit={handleSubmit} className="login-form">
          <input type="password" value={current} onChange={e => setCurrent(e.target.value)}
            placeholder="Current password" required />
          <div>
            <input type="password" value={newPw} onChange={e => setNewPw(e.target.value)}
              placeholder="New password" required />
            {newPw && (
              <div className="pw-checks">
                <span className={checks.length ? 'pw-ok' : ''}>8+ characters</span>
                <span className={checks.uppercase ? 'pw-ok' : ''}>1 uppercase</span>
                <span className={checks.number ? 'pw-ok' : ''}>1 number</span>
                <span className={checks.special ? 'pw-ok' : ''}>1 special character</span>
              </div>
            )}
          </div>
          <button type="submit" className="btn-brand login-btn" disabled={loading || !current || !isPasswordValid(newPw)}>
            {loading ? 'Updating...' : 'Update Password'}
          </button>
        </form>
        {error && <p className="login-error">{error}</p>}
        <button className="login-resend" onClick={onBack}>Cancel</button>
      </div>
    </div>
  );
}
