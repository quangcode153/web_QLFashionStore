import React from 'react';
import { BrowserRouter, Link } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import AppRoutes from './routes/AppRoutes';

function Navigation() {
  const { user, isAuthenticated, logout } = useAuth();

  return (
    <nav style={{
      background: '#ffffff',
      borderBottom: '1px solid var(--border)',
      padding: '16px 0',
      boxShadow: 'var(--shadow-sm)'
    }}>
      <div className="container" style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
      }}>
        <Link to="/" style={{ fontSize: '1.4rem', fontWeight: 800, color: 'var(--primary)' }}>
          Fashion<span style={{ color: 'var(--accent)' }}>Store</span>
        </Link>

        <div style={{ display: 'flex', gap: '20px', alignItems: 'center' }}>
          <Link to="/" style={{ fontWeight: 500 }}>Trang Chủ</Link>
          {isAuthenticated ? (
            <>
              {(user?.vaiTro === 'Quản lý' || user?.vaiTro === 'Thu ngân' || user?.vaiTro === 'Kho') && (
                <Link to="/admin/dashboard" style={{ fontWeight: 600, color: 'var(--accent)' }}>
                  Quản Trị ({user.vaiTro})
                </Link>
              )}
              <span style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                {user?.tenNhanVien}
              </span>
              <button
                onClick={logout}
                style={{
                  padding: '6px 14px',
                  background: 'var(--primary)',
                  color: '#fff',
                  borderRadius: '6px',
                  fontSize: '0.875rem'
                }}
              >
                Thoát
              </button>
            </>
          ) : (
            <Link
              to="/login"
              style={{
                padding: '8px 18px',
                background: 'var(--primary)',
                color: '#fff',
                borderRadius: '6px',
                fontWeight: 500,
                fontSize: '0.9rem'
              }}
            >
              Đăng Nhập
            </Link>
          )}
        </div>
      </div>
    </nav>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
          <Navigation />
          <main style={{ flex: 1 }}>
            <AppRoutes />
          </main>
          <footer style={{
            background: '#ffffff',
            borderTop: '1px solid var(--border)',
            padding: '24px 0',
            textAlign: 'center',
            color: 'var(--text-muted)',
            fontSize: '0.875rem'
          }}>
            <div className="container">
              &copy; 2026 FashionStore. Đồ án BTL Phân Tích & Thiết Kế Yêu Cầu.
            </div>
          </footer>
        </div>
      </AuthProvider>
    </BrowserRouter>
  );
}
