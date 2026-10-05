import React from 'react';
import { Routes, Route } from 'react-router-dom';
import HomePage from '../pages/storefront/HomePage';
import LoginPage from '../pages/auth/LoginPage';
import DashboardPage from '../pages/admin/DashboardPage';
import { RoleGuard } from './RoleGuard';

export default function AppRoutes() {
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />

      {/* Admin Protected routes (Role-based) */}
      <Route element={<RoleGuard allowedRoles={['Quản lý', 'Thu ngân', 'Kho']} />}>
        <Route path="/admin/dashboard" element={<DashboardPage />} />
      </Route>

      {/* 404 Fallback */}
      <Route path="*" element={<div style={{ padding: '60px', textAlign: 'center' }}>404 - Trang không tồn tại</div>} />
    </Routes>
  );
}
