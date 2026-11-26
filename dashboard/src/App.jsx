import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from './context/AuthContext';
import Dashboard from './pages/Dashboard';
import Login from './pages/Login';
import Opportunities from './pages/Opportunities';
import OpportunityDetail from './pages/OpportunityDetail';
import Processing from './pages/Processing';
import Report from './pages/Report';
import Reviews from './pages/Reviews';
import SubmitIdea from './pages/SubmitIdea';
import ChangePassword from './pages/ChangePassword';
import UserDashboard from './pages/UserDashboard';
import { fetchJson, subscribeSSE } from './hooks/useApi';
import './styles.css';

function UserDropdown({ user, onLogout, onChangePassword }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    const close = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  return (
    <div className="nav-user" ref={ref}>
      <button className="nav-user-btn" onClick={() => setOpen(!open)}>
        {user.email?.split('@')[0]}
        <svg width="10" height="10" viewBox="0 0 10 10" fill="none"><path d="M2.5 4l2.5 2.5L7.5 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
      </button>
      {open && (
        <div className="nav-user-dropdown">
          <div className="nav-user-email">{user.email}</div>
          <div className="nav-user-divider" />
          <button className="nav-user-dropdown-item" onClick={() => { setOpen(false); onChangePassword(); }}>Change Password</button>
          <button className="nav-user-dropdown-item" onClick={() => { setOpen(false); onLogout(); }}>Sign Out</button>
        </div>
      )}
    </div>
  );
}

function App() {
  const { isAuthenticated, user, logout } = useAuth();

  // Handle callbacks — verify / reset / public share from links
  const params = new URLSearchParams(window.location.search);
  const hasResetCallback = params.has('reset');
  const hasVerifyCallback = params.has('verify');
  const shareToken = params.get('share');
  const defaultPage = isAuthenticated ? (user?.isAdmin ? 'dashboard' : 'user-home') : 'login';
  const [page, setPage] = useState(
    shareToken ? 'public-report' :
    hasResetCallback ? 'reset-password' :
    hasVerifyCallback ? 'login' :
    defaultPage
  );

  // After login, redirect to user-home (but NOT if on reset-password page)
  useEffect(() => {
    if (isAuthenticated && page === 'login') setPage('user-home');
  }, [isAuthenticated, page]);

  const [previousPage, setPreviousPage] = useState('dashboard');
  const [selectedId, setSelectedId] = useState(null);
  const [refreshTick, setRefreshTick] = useState(0);
  const [watchingCount, setWatchingCount] = useState(0);
  const [reviewCount, setReviewCount] = useState(0);
  const [lastEvent, setLastEvent] = useState(null);
  const [toast, setToast] = useState(null);

  const showToast = useCallback((message, type = 'success') => {
    setToast({ message, type, time: Date.now() });
    setTimeout(() => setToast(null), 4000);
  }, []);

  const refresh = useCallback(() => setRefreshTick(t => t + 1), []);

  useEffect(() => {
    const loadCounts = () => {
      fetchJson('/dashboard/summary').then(s => {
        setWatchingCount(s.watching || 0);
        setReviewCount((s.deployed || 0) + (s.prototypeBuilt || 0) + (s.pendingReview || 0));
      }).catch(console.error);
    };
    loadCounts();

    const unsub = subscribeSSE((type, data) => {
      setLastEvent({ type, data, time: Date.now() });
      refresh();
      loadCounts();
    });
    return unsub;
  }, [refresh]);

  const openDetail = (id) => { setPreviousPage(page); setSelectedId(id); setPage('detail'); };
  const openReport = (id) => { setPreviousPage(page); setSelectedId(id); setPage('report'); };
  const openProcessing = (id) => { setSelectedId(id); setPage('processing'); };
  const goBack = () => { setPage(previousPage); if (previousPage !== 'detail' && previousPage !== 'report') setSelectedId(null); };

  const handleLogout = () => { logout(); setPage('login'); };

  // Public shared report — works without auth
  if (page === 'public-report' && shareToken) {
    return (
      <div className="app">
        <main className="main">
          <Report id={0} readOnly={true} shareToken={shareToken} />
        </main>
      </div>
    );
  }

  // Not authenticated — only show login/reset/verify pages
  if (!isAuthenticated) {
    return (
      <div className="app">
        <main className="main">
          <Login />
        </main>
      </div>
    );
  }

  return (
    <div className="app">
      <nav className="nav">
        <div className="nav-brand" onClick={() => setPage(user?.isAdmin ? 'dashboard' : 'user-home')} style={{ cursor: 'pointer' }}>
          <span className="nav-icon">&#9889;</span>
          <span className="nav-title">ForgeAI</span>
        </div>
        <div className="nav-links">
          {/* User tabs */}
          {isAuthenticated && (
            <button className={page === 'user-home' ? 'active' : ''} onClick={() => setPage('user-home')}>
              <svg className="nav-tab-icon" width="20" height="20" viewBox="0 0 20 20" fill="none"><path d="M3 10h4v7H3zM8 6h4v11H8zM13 3h4v14h-4z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
              <span className="nav-tab-label">My Ideas</span>
            </button>
          )}
          <button className={page === 'submit' ? 'active' : ''} onClick={() => setPage(isAuthenticated ? 'submit' : 'login')}>
            <svg className="nav-tab-icon" width="20" height="20" viewBox="0 0 20 20" fill="none"><path d="M10 4v12M4 10h12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
            <span className="nav-tab-label">Submit</span>
          </button>

          {/* Admin-only tabs */}
          {user?.isAdmin && (
            <>
              <div className="nav-divider" />
              <button className={page === 'dashboard' ? 'active' : ''} onClick={() => setPage('dashboard')}>
                <svg className="nav-tab-icon" width="20" height="20" viewBox="0 0 20 20" fill="none"><path d="M3 10h4v7H3zM8 6h4v11H8zM13 3h4v14h-4z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
                <span className="nav-tab-label">Pipeline</span>
              </button>
              <button className={page === 'opportunities' ? 'active' : ''} onClick={() => setPage('opportunities')}>
                <svg className="nav-tab-icon" width="20" height="20" viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="7" stroke="currentColor" strokeWidth="1.5"/><path d="M10 6v4l3 2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
                <span className="nav-tab-label">Opportunities</span>
                {watchingCount > 0 && <span className="nav-badge">{watchingCount}</span>}
              </button>
              <button className={page === 'reviews' ? 'active' : ''} onClick={() => setPage('reviews')}>
                <svg className="nav-tab-icon" width="20" height="20" viewBox="0 0 20 20" fill="none"><path d="M4 5h12M4 10h12M4 15h8" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/></svg>
                <span className="nav-tab-label">Reviews</span>
                {reviewCount > 0 && <span className="nav-badge alert">{reviewCount}</span>}
              </button>
            </>
          )}
          {isAuthenticated ? (
            <UserDropdown user={user} onLogout={handleLogout} onChangePassword={() => setPage('change-password')} />
          ) : (
            <button className={page === 'login' ? 'active' : ''} onClick={() => setPage('login')}>
              <span className="nav-tab-label">Sign In</span>
            </button>
          )}
        </div>
      </nav>

      <main className="main">
        {page === 'dashboard' && <Dashboard refreshTick={refreshTick} onViewOpportunity={openDetail} />}
        {page === 'opportunities' && <Opportunities onSelect={openDetail} />}
        {page === 'detail' && selectedId && <OpportunityDetail id={selectedId} onBack={goBack} onRefresh={refresh} onToast={showToast} onViewReport={openReport} backLabel={previousPage === 'dashboard' ? 'Pipeline' : previousPage === 'reviews' ? 'Reviews' : 'Opportunities'} />}
        {page === 'report' && selectedId && <Report id={selectedId} onBack={goBack} onRefresh={refresh} />}
        {page === 'reviews' && <Reviews onSelect={openDetail} />}
        {page === 'processing' && selectedId && <Processing id={selectedId} onReport={openReport} onBack={() => setPage('submit')} />}
        {page === 'login' && <Login />}
        {page === 'reset-password' && <Login />}
        {page === 'change-password' && isAuthenticated && <ChangePassword onBack={() => setPage('user-home')} />}
        {page === 'user-home' && isAuthenticated && <UserDashboard onSubmit={() => setPage('submit')} onViewReport={openReport} />}
        {page === 'submit' && isAuthenticated && <SubmitIdea onBack={() => setPage('user-home')} onToast={showToast} onProcessing={openProcessing} />}
        {page === 'submit' && !isAuthenticated && <Login />}
      </main>

      {lastEvent && (Date.now() - lastEvent.time < 5000) && (
        <div className="sse-toast">
          {lastEvent.type.replace('.', ' ')}: {lastEvent.data?.title || lastEvent.data?.Title || ''}
        </div>
      )}

      {toast && (
        <div className={`toast toast-${toast.type}`} key={toast.time}>
          <span className="toast-icon">
            {toast.type === 'success' ? '\u2713' : toast.type === 'error' ? '\u2717' : '\u2139'}
          </span>
          <span className="toast-message">{toast.message}</span>
        </div>
      )}
    </div>
  );
}

export default App;
