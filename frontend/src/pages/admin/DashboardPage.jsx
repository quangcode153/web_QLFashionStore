import React, { useState, useEffect } from 'react';
import api from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';

export default function DashboardPage() {
  const { user, logout } = useAuth();
  const [lowStockItems, setLowStockItems] = useState([]);
  const [orders, setOrders] = useState([]);

  useEffect(() => {
    // Tải danh sách cảnh báo hết hàng
    api.get('/warehouse/low-stock')
      .then((res) => { if (res.success) setLowStockItems(res.data || []); })
      .catch((err) => console.error(err));

    // Tải danh sách đơn hàng
    api.get('/orders')
      .then((res) => { if (res.success) setOrders(res.data || []); })
      .catch((err) => console.error(err));
  }, []);

  return (
    <div className="container" style={{ padding: '30px 20px' }}>
      {/* Header bar */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingBottom: '20px',
        borderBottom: '1px solid var(--border)',
        marginBottom: '30px'
      }}>
        <div>
          <h1 style={{ fontSize: '1.75rem', fontWeight: 700 }}>Bảng Điều Khiển Quản Trị</h1>
          <p style={{ color: 'var(--text-muted)' }}>
            Xin chào, <strong>{user?.tenNhanVien}</strong> ({user?.vaiTro})
          </p>
        </div>
        <button
          onClick={logout}
          style={{
            padding: '8px 16px',
            background: 'var(--danger)',
            color: '#fff',
            borderRadius: '6px',
            fontWeight: 500
          }}
        >
          Đăng xuất
        </button>
      </div>

      {/* Metrics Row */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: '20px',
        marginBottom: '30px'
      }}>
        <div style={{ background: '#fff', padding: '20px', borderRadius: 'var(--radius)', border: '1px solid var(--border)' }}>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>Tổng số đơn hàng</p>
          <h2 style={{ fontSize: '1.8rem', fontWeight: 700 }}>{orders.length}</h2>
        </div>
        <div style={{ background: '#fff', padding: '20px', borderRadius: 'var(--radius)', border: '1px solid var(--border)' }}>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>Cảnh báo hết hàng</p>
          <h2 style={{ fontSize: '1.8rem', fontWeight: 700, color: 'var(--danger)' }}>{lowStockItems.length}</h2>
        </div>
      </div>

      {/* Low stock alert section */}
      <div style={{ background: '#fff', padding: '24px', borderRadius: 'var(--radius)', border: '1px solid var(--border)' }}>
        <h3 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: '16px' }}>
          Cảnh Báo Tồn Kho Thấp (&le; 10 sản phẩm)
        </h3>
        {lowStockItems.length === 0 ? (
          <p style={{ color: 'var(--text-muted)' }}>Kho hàng hiện tại an toàn.</p>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--border)' }}>
                <th style={{ padding: '10px' }}>Tên sản phẩm</th>
                <th style={{ padding: '10px' }}>Mã SKU</th>
                <th style={{ padding: '10px' }}>Màu sắc</th>
                <th style={{ padding: '10px' }}>Size</th>
                <th style={{ padding: '10px' }}>Tồn kho</th>
              </tr>
            </thead>
            <tbody>
              {lowStockItems.map((item) => (
                <tr key={item.maBienThe} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '10px', fontWeight: 500 }}>{item.tenSanPham}</td>
                  <td style={{ padding: '10px' }}>{item.maSku}</td>
                  <td style={{ padding: '10px' }}>{item.mauSac}</td>
                  <td style={{ padding: '10px' }}>{item.kichCo}</td>
                  <td style={{ padding: '10px', color: 'var(--danger)', fontWeight: 700 }}>
                    {item.soLuongTon}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
