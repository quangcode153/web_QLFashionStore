import React from 'react';
import { Routes, Route } from 'react-router-dom';
import HomePage from '../pages/storefront/HomePage';
import LoginPage from '../pages/auth/LoginPage';
import DashboardPage from '../pages/admin/DashboardPage';
import { RoleGuard } from './RoleGuard';

export default function AppRoutes() {
  return (
    <Routes>
      {/* 1. Tuyến đường công khai (Dành cho Khách hàng & Khách vãng lai) */}
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />

      {/* 2. Tuyến đường phân quyền dành cho Quản lý chi nhánh & Admin chuỗi */}
      <Route element={<RoleGuard allowedRoles={['Admin chuỗi', 'Quản lý chi nhánh', 'Nhân viên bán hàng']} />}>
        <Route path="/admin/dashboard" element={<DashboardPage />} />
      </Route>

      {/* 404 Fallback */}
      <Route path="*" element={<div style={{ padding: '60px', textAlign: 'center' }}>404 - Trang không tồn tại</div>} />
    </Routes>
  );
}
