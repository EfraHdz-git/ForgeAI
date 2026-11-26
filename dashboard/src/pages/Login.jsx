import { useState, useEffect } from 'react';
import { postJson } from '../hooks/useApi';
import { useAuth } from '../context/AuthContext';

import { passwordChecks, isPasswordValid } from '../utils/password';
import DemoShowcase from '../components/DemoShowcase';
import PipelinePanel from '../components/PipelinePanel';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function Login() {
  const { login } = useAuth();
  const [mode, setMode] = useState('login'); // login | signup | forgot | verify-sent | reset-sent
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [emailTouched, setEmailTouched] = useState(false);

  // Handle verify callback (?verify=token)
  const verifyToken = new URLSearchParams(window.location.search).get('verify');
  const [verifying, setVerifying] = useState(!!verifyToken);

  // Handle reset callback (?reset=token)
  const resetToken = new URLSearchParams(window.location.search).get('reset');
  const [newPassword, setNewPassword] = useState('');
  const [resetting, setResetting] = useState(false);

  // Auto-verify email on load
  useEffect(() => {
    if (!verifyToken) return;
    postJson('/auth/verify-email', { token: verifyToken })
      .then(res => {
        login(res.token, res.user);
        window.history.replaceState({}, '', window.location.pathname);
      })
      .catch(() => {
        setVerifying(false);
        setError('Invalid or expired verification link.');
      });
  }, [verifyToken, login]);

  if (verifying) {
    return (
      <div className="login-page">
        <div className="login-card">
          <div className="login-icon">&#9889;</div>
          <h2>Verifying your email...</h2>
        </div>
      </div>
    );
  }

  const emailValid = EMAIL_RE.test(email.trim());
  const checks = passwordChecks(password);
  const passwordValid = isPasswordValid(password);

  const switchMode = (m) => { setMode(m); setError(null); setPassword(''); };

  const handleSignup = async (e) => {
    e.preventDefault();
    if (!emailValid || !passwordValid) return;
    setLoading(true);
    setError(null);
    try {
      const res = await postJson('/auth/signup', { email: email.trim(), password });
      if (res.needsVerification) setMode('verify-sent');
    } catch (err) {
      const msg = err.message;
      if (msg.includes('409')) setError('An account with this email already exists.');
      else setError('Signup failed. Please try again.');
    }
    setLoading(false);
  };

  const handleLogin = async (e) => {
    e.preventDefault();
    if (!email.trim() || !password) return;
    setLoading(true);
    setError(null);
    try {
      const res = await postJson('/auth/login', { email: email.trim(), password });
      login(res.token, res.user);
    } catch (err) {
      const msg = err.message;
      if (msg.includes('403')) setError('Please verify your email before signing in. Check your inbox.');
      else setError('Invalid email or password.');
    }
    setLoading(false);
  };

  const handleForgot = async (e) => {
    e.preventDefault();
    if (!emailValid) return;
    setLoading(true);
    setError(null);
    try {
      await postJson('/auth/forgot-password', { email: email.trim() });
      setMode('reset-sent');
    } catch {
      setError('Failed to send reset link.');
    }
    setLoading(false);
  };

  const handleReset = async (e) => {
    e.preventDefault();
    const resetChecks = passwordChecks(newPassword);
    if (!(resetChecks.length && resetChecks.uppercase && resetChecks.special)) {
      setError('Password does not meet requirements.');
      return;
    }
    setResetting(true);
    setError(null);
    try {
      const res = await postJson('/auth/reset-password', { token: resetToken, password: newPassword });
      login(res.token, res.user);
      window.history.replaceState({}, '', window.location.pathname);
    } catch {
      setError('Invalid or expired reset link.');
    }
    setResetting(false);
  };

  // Password reset form
  if (resetToken) {
    const resetChecks = passwordChecks(newPassword);
    return (
      <div className="login-page">
        <div className="login-card">
          <div className="login-brand"><span className="login-brand-icon">&#9889;</span><span className="login-brand-name">ForgeAI</span></div>
          <h2>Set a new password</h2>
          <form onSubmit={handleReset} className="login-form">
            <input type="password" value={newPassword} onChange={e => setNewPassword(e.target.value)} placeholder="New password" autoFocus required />
            <div className="pw-checks">
              <span className={resetChecks.length ? 'pw-ok' : ''}>8+ characters</span>
              <span className={resetChecks.uppercase ? 'pw-ok' : ''}>1 uppercase</span>
              <span className={resetChecks.number ? 'pw-ok' : ''}>1 number</span>
              <span className={resetChecks.special ? 'pw-ok' : ''}>1 special character</span>
            </div>
            <button type="submit" className="btn-brand login-btn" disabled={resetting || !(resetChecks.length && resetChecks.uppercase && resetChecks.special)}>
              {resetting ? 'Resetting...' : 'Reset Password'}
            </button>
          </form>
          {error && <p className="login-error">{error}</p>}
        </div>
      </div>
    );
  }

  // Verify sent
  if (mode === 'verify-sent') {
    return (
      <div className="login-page">
        <div className="login-card">
          <div className="login-icon">&#9993;</div>
          <h2>Verify your email</h2>
          <p className="login-subtitle">We sent a verification link to <strong>{email}</strong></p>
          <p className="login-hint">Click the link in your email to activate your account. Check spam if you don't see it.</p>
          <button className="login-resend" onClick={() => switchMode('login')}>Back to sign in</button>
        </div>
      </div>
    );
  }

  // Reset sent
  if (mode === 'reset-sent') {
    return (
      <div className="login-page">
        <div className="login-card">
          <div className="login-icon">&#9993;</div>
          <h2>Check your email</h2>
          <p className="login-subtitle">If an account exists for <strong>{email}</strong>, we sent a password reset link.</p>
          <button className="login-resend" onClick={() => switchMode('login')}>Back to sign in</button>
        </div>
      </div>
    );
  }

  return (
    <div className="login-page login-page-split">
      <div className="login-col">
      <div className="login-card">
        <div className="login-brand"><span className="login-brand-icon">&#9889;</span><span className="login-brand-name">ForgeAI</span></div>

        <div className="login-tabs">
          <button className={mode === 'login' ? 'active' : ''} onClick={() => switchMode('login')}>Sign In</button>
          <button className={mode === 'signup' ? 'active' : ''} onClick={() => switchMode('signup')}>Create Account</button>
        </div>

        {mode === 'login' && (
          <>
            <form onSubmit={handleLogin} className="login-form">
              <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="Email" autoFocus required />
              <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Password" required />
              <button type="submit" className="btn-brand login-btn" disabled={loading}>
                {loading ? 'Signing in...' : 'Sign In'}
              </button>
            </form>
            <button className="login-forgot" onClick={() => switchMode('forgot')}>Forgot password?</button>
          </>
        )}

        {mode === 'signup' && (
          <form onSubmit={handleSignup} className="login-form">
            <div>
              <input type="email" value={email} onChange={e => setEmail(e.target.value)}
                onBlur={() => setEmailTouched(true)} placeholder="Email" required />
              {emailTouched && email && !emailValid && <p className="login-field-error">Enter a valid email address</p>}
            </div>
            <div>
              <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Password" required />
              {password && (
                <div className="pw-checks">
                  <span className={checks.length ? 'pw-ok' : ''}>8+ characters</span>
                  <span className={checks.uppercase ? 'pw-ok' : ''}>1 uppercase</span>
                  <span className={checks.number ? 'pw-ok' : ''}>1 number</span>
                  <span className={checks.special ? 'pw-ok' : ''}>1 special character</span>
                </div>
              )}
            </div>
            <button type="submit" className="btn-brand login-btn" disabled={loading || !emailValid || !passwordValid}>
              {loading ? 'Creating account...' : 'Create Account'}
            </button>
          </form>
        )}

        {mode === 'forgot' && (
          <>
            <p className="login-subtitle">Enter your email and we'll send a reset link.</p>
            <form onSubmit={handleForgot} className="login-form">
              <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="Email" autoFocus required />
              <button type="submit" className="btn-brand login-btn" disabled={loading || !emailValid}>
                {loading ? 'Sending...' : 'Send Reset Link'}
              </button>
            </form>
            <button className="login-resend" onClick={() => switchMode('login')}>Back to sign in</button>
          </>
        )}

        {error && <p className="login-error">{error}</p>}
        <p className="login-footer-text">Your ideas are private and never shared without your permission.</p>
      </div>
      <DemoShowcase />
      </div>
      <PipelinePanel />
    </div>
  );
}
