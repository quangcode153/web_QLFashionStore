import { executeQuery } from '../config/db.js';

/**
 * 1. Tìm kiếm sản phẩm theo từ khóa (tên sản phẩm, danh mục, chất liệu, thương hiệu)
 */
export async function searchProducts(keyword) {
  try {
    const query = `
      SELECT TOP 10 
        sp.ma_san_pham,
        sp.ten_san_pham,
        sp.chat_lieu,
        dm.ten_danh_muc,
        th.ten_thuong_hieu,
        MIN(bt.gia_ban) AS gia_thap_nhat,
        MAX(bt.gia_ban) AS gia_cao_nhat,
        SUM(ISNULL(tk.so_luong_ton, 0)) AS tong_ton_kho
      FROM SanPham sp
      LEFT JOIN DanhMuc dm ON sp.ma_danh_muc = dm.ma_danh_muc
      LEFT JOIN ThuongHieu th ON sp.ma_thuong_hieu = th.ma_thuong_hieu
      LEFT JOIN BienTheSanPham bt ON sp.ma_san_pham = bt.ma_san_pham
      LEFT JOIN TonKhoChiNhanh tk ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
      WHERE (sp.da_xoa = 0 OR sp.da_xoa IS NULL)
        AND (
          sp.ten_san_pham LIKE @keyword 
          OR dm.ten_danh_muc LIKE @keyword 
          OR sp.chat_lieu LIKE @keyword
          OR th.ten_thuong_hieu LIKE @keyword
        )
      GROUP BY sp.ma_san_pham, sp.ten_san_pham, sp.chat_lieu, dm.ten_danh_muc, th.ten_thuong_hieu
    `;
    const rows = await executeQuery(query, { keyword: `%${keyword || ''}%` });
    return rows;
  } catch (error) {
    console.error('[InventoryService] Lỗi searchProducts:', error.message);
    return { error: 'Không thể truy vấn CSDL: ' + error.message };
  }
}

/**
 * 2. Lấy danh sách tất cả biến thể (màu sắc, size, giá, tồn kho) của sản phẩm
 */
export async function getProductVariants(productName) {
  try {
    const query = `
      SELECT 
        sp.ma_san_pham,
        sp.ten_san_pham,
        sp.chat_lieu,
        bt.ma_bien_the,
        bt.ma_sku,
        bt.mau_sac,
        bt.kich_co,
        bt.gia_ban,
        ISNULL(tk.so_luong_ton, 0) AS so_luong_ton,
        CASE WHEN ISNULL(tk.so_luong_ton, 0) > 0 THEN N'Còn hàng' ELSE N'Hết hàng' END AS trang_thai_hang
      FROM SanPham sp
      INNER JOIN BienTheSanPham bt ON sp.ma_san_pham = bt.ma_san_pham
      LEFT JOIN TonKhoChiNhanh tk ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
      WHERE (sp.da_xoa = 0 OR sp.da_xoa IS NULL)
        AND sp.ten_san_pham LIKE @keyword
      ORDER BY bt.mau_sac, bt.kich_co
    `;
    const rows = await executeQuery(query, { keyword: `%${productName || ''}%` });
    return rows;
  } catch (error) {
    console.error('[InventoryService] Lỗi getProductVariants:', error.message);
    return { error: 'Không thể truy vấn biến thể: ' + error.message };
  }
}

/**
 * 3. Kiểm tra chi tiết số lượng tồn kho theo sản phẩm + màu sắc + kích cỡ
 */
export async function checkStock({ productName, color, size }) {
  try {
    let query = `
      SELECT 
        sp.ten_san_pham,
        bt.ma_sku,
        bt.mau_sac,
        bt.kich_co,
        bt.gia_ban,
        ISNULL(tk.so_luong_ton, 0) AS so_luong_ton,
        CASE 
          WHEN ISNULL(tk.so_luong_ton, 0) > 0 THEN N'Còn hàng'
          ELSE N'Hết hàng'
        END AS trang_thai_kho
      FROM SanPham sp
      INNER JOIN BienTheSanPham bt ON sp.ma_san_pham = bt.ma_san_pham
      LEFT JOIN TonKhoChiNhanh tk ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
      WHERE (sp.da_xoa = 0 OR sp.da_xoa IS NULL)
        AND sp.ten_san_pham LIKE @productName
    `;

    const inputs = { productName: `%${productName || ''}%` };

    if (color) {
      inputs.color = `%${color}%`;
      query += ` AND bt.mau_sac LIKE @color`;
    }

    if (size) {
      inputs.size = size;
      query += ` AND bt.kich_co = @size`;
    }

    const rows = await executeQuery(query, inputs);
    return rows;
  } catch (error) {
    console.error('[InventoryService] Lỗi checkStock:', error.message);
    return { error: 'Lỗi kiểm tra kho: ' + error.message };
  }
}

/**
 * 4. Lấy các chương trình khuyến mãi & Voucher đang hoạt động
 */
export async function getActivePromotions() {
  try {
    const query = `
      SELECT 
        ten_khuyen_mai,
        ma_giam_gia,
        phan_tram_giam,
        so_tien_giam,
        mua_toi_thieu,
        ngay_ket_thuc,
        so_luong_dung
      FROM ChuongTrinhKhuyenMai
      WHERE ngay_ket_thuc >= CAST(GETDATE() AS DATE)
        AND so_luong_dung > 0
    `;
    const rows = await executeQuery(query);
    return rows;
  } catch (error) {
    console.error('[InventoryService] Lỗi getActivePromotions:', error.message);
    return { error: 'Lỗi lấy khuyến mãi: ' + error.message };
  }
}
