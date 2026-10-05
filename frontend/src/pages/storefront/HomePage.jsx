import React, { useState, useEffect } from 'react';
import api from '../../services/api';

export default function HomePage() {
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get('/products')
      .then((res) => {
        if (res.success) setProducts(res.data || []);
      })
      .catch((err) => console.error(err))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="container" style={{ padding: '40px 20px' }}>
      {/* Hero Banner */}
      <div style={{
        background: 'linear-gradient(135deg, #1e293b 0%, #0f172a 100%)',
        color: '#ffffff',
        padding: '60px 40px',
        borderRadius: 'var(--radius)',
        marginBottom: '40px',
        textAlign: 'center'
      }}>
        <h1 style={{ fontSize: '2.5rem', fontWeight: 700, marginBottom: '16px' }}>
          Thời Trang Cao Cấp FashionStore
        </h1>
        <p style={{ fontSize: '1.1rem', color: '#cbd5e1', maxWidth: '600px', margin: '0 auto 24px' }}>
          Khám phá bộ sưu tập mới nhất với chất liệu cao cấp và kiểu dáng thời thượng.
        </p>
      </div>

      {/* Product Grid */}
      <h2 style={{ fontSize: '1.75rem', fontWeight: 600, marginBottom: '24px' }}>
        Sản Phẩm Nổi Bật
      </h2>

      {loading ? (
        <div style={{ textAlign: 'center', padding: '40px' }}>Đang tải sản phẩm...</div>
      ) : (
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
          gap: '24px'
        }}>
          {products.map((p) => (
            <div key={p.maSanPham} style={{
              background: '#ffffff',
              borderRadius: 'var(--radius)',
              border: '1px solid var(--border)',
              overflow: 'hidden',
              boxShadow: 'var(--shadow-sm)'
            }}>
              <div style={{ height: '240px', background: '#e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                {p.anhDaiDien ? (
                  <img src={p.anhDaiDien} alt={p.tenSanPham} style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                ) : (
                  <span style={{ color: '#94a3b8' }}>Ảnh sản phẩm</span>
                )}
              </div>
              <div style={{ padding: '16px' }}>
                <span className="badge badge-success" style={{ marginBottom: '8px' }}>{p.tenDanhMuc}</span>
                <h3 style={{ fontSize: '1.1rem', fontWeight: 600, margin: '8px 0' }}>{p.tenSanPham}</h3>
                <p style={{ color: 'var(--accent)', fontWeight: 700, fontSize: '1.1rem' }}>
                  {p.giaThapNhat ? p.giaThapNhat.toLocaleString('vi-VN') + ' đ' : 'Liên hệ'}
                </p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
