import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export const RoleGuard = ({ allowedRoles }) => {
  const { user, loading, isAuthenticated } = useAuth();

  if (loading) {
    return <div style={{ padding: '40px', textAlign: 'center' }}>Đang tải...</div>;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user?.vaiTro)) {
    return (
      <div style={{ padding: '60px 20px', textAlign: 'center' }}>
        <h2>403 - Bạn không có quyền truy cập trang này</h2>
        <p>Vai trò hiện tại của bạn là: <strong>{user?.vaiTro}</strong></p>
        <a href="/" style={{ color: 'var(--accent)', textDecoration: 'underline' }}>Quay về trang chủ</a>
      </div>
    );
  }

  return <Outlet />;
};
