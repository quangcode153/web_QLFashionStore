import {
  searchProducts,
  getProductVariants,
  checkStock,
  getActivePromotions,
} from './inventoryService.js';
import { executeQuery } from '../config/db.js';

/**
 * Tính toán size áo/quần chuẩn thời trang dựa trên chiều cao và cân nặng
 */
export function calculateClothingSize(heightCm, weightKg) {
  let size = 'M';
  let advice = '';

  if (heightCm < 162 || weightKg < 52) {
    size = 'S';
    advice = 'Size S sẽ vừa vặn và ôm gọn với vóc dáng của bạn.';
  } else if (heightCm <= 170 && weightKg <= 64) {
    size = 'M';
    advice = 'Size M là kích cỡ chuẩn vừa đẹp, thoải mái vận động.';
  } else if (heightCm <= 177 && weightKg <= 74) {
    size = 'L';
    advice = 'Size L sẽ rất vừa vặn, form dáng chuẩn đẹp.';
  } else if (heightCm <= 184 && weightKg <= 84) {
    size = 'XL';
    advice = 'Size XL sẽ giúp bạn mặc thoải mái, rộng rãi tôn dáng.';
  } else {
    size = 'XXL';
    advice = 'Size XXL hoặc form Oversize sẽ thoải mái nhất cho bạn.';
  }

  return { size, advice };
}

/**
 * Trích xuất chiều cao (cm) và cân nặng (kg) từ văn bản người dùng
 */
export function extractMeasurements(text) {
  let height = null;
  let weight = null;

  // Nhận diện chiều cao: 1m70, 1.70m, 1m7, 170cm, cao 170...
  const heightMatch1 = text.match(/(\d)[m\.,](\d{1,2})/i);
  const heightMatch2 = text.match(/(\d{3})\s*(?:cm)?/i);
  const heightKeyword = text.match(/cao\s*(\d{2,3})/i);

  if (heightMatch1) {
    const meters = parseInt(heightMatch1[1], 10);
    let cm = heightMatch1[2];
    if (cm.length === 1) cm = cm + '0';
    height = meters * 100 + parseInt(cm, 10);
  } else if (heightKeyword) {
    height = parseInt(heightKeyword[1], 10);
    if (height < 100) height += 100;
  } else if (heightMatch2) {
    const val = parseInt(heightMatch2[1], 10);
    if (val >= 140 && val <= 210) height = val;
  }

  // Nhận diện cân nặng: 65kg, 65 kí, 65 ký, 65 cân, nặng 65...
  const weightMatch = text.match(/(\d{2,3})\s*(?:kg|kí|ký|cân)/i) || text.match(/nặng\s*(\d{2,3})/i);
  if (weightMatch) {
    const val = parseInt(weightMatch[1], 10);
    if (val >= 35 && val <= 140) weight = val;
  }

  return { height, weight };
}

/**
 * Xử lý tin nhắn người dùng bằng Rule-Based Offline Engine
 */
export async function processOfflineRuleBased(message) {
  const lowerMsg = message.toLowerCase();

  // 1. KIỂM TRA PHẠM VI (GUARDRAIL OFFLINE)
  const outOfScopePatterns = [
    /phương trình/i, /giải toán/i, /tính tích phân/i, /đạo hàm/i,
    /viết code/i, /lập trình/i, /python/i, /javascript/i, /c#/i,
    /thời tiết/i, /chính trị/i, /chiến tranh/i, /bóng đá hôm nay/i,
    /ai là tổng thống/i, /dịch tiếng anh/i
  ];

  for (const regex of outOfScopePatterns) {
    if (regex.test(message)) {
      return {
        reply: "Dạ, em là Stylist AI của QF NOIR. Em chỉ có thể hỗ trợ quý khách thông tin về các mẫu thời trang, màu sắc, size và số lượng tồn kho thôi ạ. Quý khách đang quan tâm đến sản phẩm nào để em tư vấn ngay ạ?",
        toolsUsed: [{ name: 'Offline_Guardrail' }],
        engine: 'Rule-Based Offline'
      };
    }
  }

  // 2. TƯ VẤN CHỌN SIZE THEO CHIỀU CAO & CÂN NẶNG
  const { height, weight } = extractMeasurements(message);
  if (height || weight) {
    const h = height || 170;
    const w = weight || 65;
    const { size, advice } = calculateClothingSize(h, w);

    let reply = `Dạ, với vóc dáng chiều cao khoảng ${h}cm và cân nặng ${w}kg, Stylist AI khuyên bạn nên chọn Size ${size} ạ!\n${advice}\n\n`;

    // Tra cứu các sản phẩm đang có size này còn hàng trong kho
    try {
      const availableItems = await executeQuery(`
        SELECT TOP 5 sp.ten_san_pham, bt.mau_sac, ISNULL(tk.so_luong_ton, 0) AS so_luong_ton, bt.gia_ban
        FROM SanPham sp
        INNER JOIN BienTheSanPham bt ON sp.ma_san_pham = bt.ma_san_pham
        LEFT JOIN TonKhoChiNhanh tk ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
        WHERE bt.kich_co = @size AND ISNULL(tk.so_luong_ton, 0) > 0
        ORDER BY tk.so_luong_ton DESC
      `, { size });

      if (availableItems && availableItems.length > 0) {
        reply += `Một số mẫu đang có sẵn Size ${size} trong kho:\n`;
        availableItems.forEach((item, idx) => {
          reply += `${idx + 1}. ${item.ten_san_pham} (Màu: ${item.mau_sac}) - Giá: ${Number(item.gia_ban).toLocaleString('vi-VN')}đ (Còn ${item.so_luong_ton} cái)\n`;
        });
        reply += `\nQuý khách ưng mẫu nào có thể báo em kiểm tra chi tiết nhé ạ!`;
      }
    } catch (e) {
      console.warn('[Offline Engine] Lỗi tra cứu mẫu theo size:', e.message);
    }

    return {
      reply,
      toolsUsed: [{ name: 'RuleBased_SizeCalculator', args: { height: h, weight: w, size } }],
      engine: 'Rule-Based Offline'
    };
  }

  // 3. TRA CỨU KHUYẾN MÃI / VOUCHER
  if (lowerMsg.includes('khuyến mãi') || lowerMsg.includes('voucher') || lowerMsg.includes('giảm giá') || lowerMsg.includes('ưu đãi') || lowerMsg.includes('mã')) {
    try {
      const promos = await getActivePromotions();
      if (promos && promos.length > 0) {
        let reply = "Dạ, hiện tại QF NOIR đang có các mã giảm giá và ưu đãi sau ạ:\n\n";
        promos.forEach((p, idx) => {
          reply += `${idx + 1}. ${p.ten_khuyen_mai}\n`;
          reply += `   - Mã: ${p.ma_giam_gia}\n`;
          if (p.so_tien_giam > 0) reply += `   - Giảm: ${Number(p.so_tien_giam).toLocaleString('vi-VN')}đ\n`;
          if (p.phan_tram_giam > 0) reply += `   - Giảm: ${p.phan_tram_giam}%\n`;
          reply += `   - Đơn tối thiểu: ${Number(p.mua_toi_thieu).toLocaleString('vi-VN')}đ (Còn ${p.so_luong_dung} lượt)\n\n`;
        });
        reply += "Quý khách có thể áp dụng mã khi thanh toán đơn hàng ạ!";
        return {
          reply,
          toolsUsed: [{ name: 'getActivePromotions' }],
          engine: 'Rule-Based Offline'
        };
      }
    } catch (e) {}
  }

  // 4. TRA CỨU SẢN PHẨM & TỒN KHO THEO TỪ KHÓA
  try {
    const products = await executeQuery(`SELECT ma_san_pham, ten_san_pham FROM SanPham WHERE da_xoa = 0 OR da_xoa IS NULL`);
    let matchedProduct = null;

    if (products && products.length > 0) {
      for (const p of products) {
        const pNameLower = p.ten_san_pham.toLowerCase();
        // Kiểm tra xem tên sản phẩm có xuất hiện trong tin nhắn khách không
        const words = pNameLower.split(' ').filter(w => w.length > 2);
        let matchCount = 0;
        words.forEach(w => {
          if (lowerMsg.includes(w)) matchCount++;
        });

        if (matchCount >= 2 || lowerMsg.includes(pNameLower)) {
          matchedProduct = p;
          break;
        }
      }
    }

    if (matchedProduct) {
      const variants = await getProductVariants(matchedProduct.ten_san_pham);
      if (variants && variants.length > 0) {
        let reply = `Dạ, mẫu ${matchedProduct.ten_san_pham} hiện đang có các biến thể trong kho như sau ạ:\n\n`;
        
        // Nhóm theo màu
        const colorMap = {};
        variants.forEach(v => {
          if (!colorMap[v.mau_sac]) colorMap[v.mau_sac] = [];
          colorMap[v.mau_sac].push(v);
        });

        for (const [color, list] of Object.entries(colorMap)) {
          const price = Number(list[0]?.gia_ban || 0).toLocaleString('vi-VN');
          reply += `Màu ${color} (Giá: ${price}đ):\n`;
          list.forEach(v => {
            const stockText = v.so_luong_ton > 0 ? `Còn ${v.so_luong_ton} chiếc` : 'Tạm hết hàng';
            reply += ` - Size ${v.kich_co}: ${stockText}\n`;
          });
          reply += `\n`;
        }

        reply += `Quý khách muốn chọn màu sắc và size nào để em hỗ trợ ạ?`;
        return {
          reply: reply.trim(),
          toolsUsed: [{ name: 'getProductVariants', args: { productName: matchedProduct.ten_san_pham } }],
          engine: 'Rule-Based Offline'
        };
      }
    }
  } catch (e) {
    console.warn('[Offline Engine] Lỗi match sản phẩm:', e.message);
  }

  // 5. CÂU HỎI CHUNG / MẶC ĐỊNH
  return {
    reply: "Dạ, em là Stylist AI của QF NOIR. Em có thể giúp bạn:\n" +
           "- Tư vấn chọn size chuẩn: Bạn cho em biết chiều cao và cân nặng nhé.\n" +
           "- Kiểm tra màu sắc và tồn kho: Bạn hỏi tên mẫu áo/quần bạn đang xem.\n" +
           "- Mã giảm giá: Hỏi về các ưu đãi, voucher hôm nay.\n\n" +
           "Bạn đang quan tâm đến mẫu nào để em hỗ trợ tư vấn ngay ạ?",
    toolsUsed: [{ name: 'Default_Greeting' }],
    engine: 'Rule-Based Offline'
  };
}
