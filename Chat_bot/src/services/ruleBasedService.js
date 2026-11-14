import {
  searchProducts,
  getProductVariants,
  checkStock,
  getActivePromotions,
} from './inventoryService.js';
import { executeQuery } from '../config/db.js';

// Danh mục sản phẩm dự phòng mặc định khi DB đang bận hoặc offline
const DEFAULT_FALLBACK_PRODUCTS = [
  { ma_san_pham: 1, ten_san_pham: 'Áo Thun Cotton Basic Nam QF' },
  { ma_san_pham: 2, ten_san_pham: 'Áo Polo Thể Thao Nam QF Pique' },
  { ma_san_pham: 3, ten_san_pham: 'Áo Thun Cổ Tròn Premium Dày Dặn' },
  { ma_san_pham: 4, ten_san_pham: 'Áo Thun Oversize Form Rộng Urban' },
  { ma_san_pham: 5, ten_san_pham: 'Áo Thun Thể Thao Dry-Ex Co Giãn' },
  { ma_san_pham: 6, ten_san_pham: 'Áo Sơ Mi Oxford Dài Tay Nam' },
  { ma_san_pham: 7, ten_san_pham: 'Áo Sơ Mi Công Sở Slimfit Chống Nhăn' },
  { ma_san_pham: 8, ten_san_pham: 'Áo Sơ Mi Linen Ngắn Tay Thoáng Mát' },
  { ma_san_pham: 10, ten_san_pham: 'Quần Jean Slimfit Nam Co Giãn' },
  { ma_san_pham: 11, ten_san_pham: 'Quần Jean Ống Suông Nam Straight' },
  { ma_san_pham: 14, ten_san_pham: 'Quần Kaki Slimfit Nam Co Giãn' },
  { ma_san_pham: 15, ten_san_pham: 'Quần Kaki Ống Đứng Nam Chinos' },
  { ma_san_pham: 18, ten_san_pham: 'Áo Khoác Bomber Gió Nam QF' },
  { ma_san_pham: 20, ten_san_pham: 'Áo Khoác Dù 2 Lớp Chống Nước QF' },
  { ma_san_pham: 22, ten_san_pham: 'Quần Tây Nam Slimfit Co Giãn Công Sở' },
  { ma_san_pham: 23, ten_san_pham: 'Quần Âu Nam Xếp Ly Phong Cách Ý' },
  { ma_san_pham: 24, ten_san_pham: 'Quần Tây Nam Cạp Cao Gurkha Lịch Lãm' },
  { ma_san_pham: 30, ten_san_pham: 'Set Đồ Bộ Thể Thao Nam Mùa Hè QF' },
  { ma_san_pham: 31, ten_san_pham: 'Quần Jogger Nỉ Thể Thao Nam Bo Gấu' },
  { ma_san_pham: 32, ten_san_pham: 'Quần Short Cargo Túi Hộp Nam' },
  { ma_san_pham: 35, ten_san_pham: 'Balo Laptop Nam QF Chống Trộm' },
  { ma_san_pham: 34, ten_san_pham: 'Túi Đeo Chéo Nam Da PU Kháng Nước' },
  { ma_san_pham: 33, ten_san_pham: 'Túi Tote Canvas Nam QF Tối Giản' },
  { ma_san_pham: 36, ten_san_pham: 'Thắt Lưng Da Bò Nam Khóa Kim' },
  { ma_san_pham: 37, ten_san_pham: 'Ví Da Nam Gấp Đôi Cao Cấp QF' },
  { ma_san_pham: 38, ten_san_pham: 'Ví Cầm Tay Clutch Nam Da Bò Thật' }
];

let cachedProducts = null;
let lastProductsFetchTime = 0;
const CACHE_TTL_MS = 10 * 60 * 1000; // Cache 10 phút

async function getCachedProducts() {
  const now = Date.now();
  if (cachedProducts && (now - lastProductsFetchTime < CACHE_TTL_MS)) {
    return cachedProducts;
  }
  try {
    const products = await executeQuery(`SELECT ma_san_pham, ten_san_pham FROM SanPham WHERE da_xoa = 0 OR da_xoa IS NULL`);
    if (products && products.length > 0) {
      cachedProducts = products;
      lastProductsFetchTime = now;
      return cachedProducts;
    }
  } catch (err) {
    console.warn('[Offline Engine] Không truy vấn được CSDL, sử dụng danh mục bộ nhớ:', err.message);
  }
  return cachedProducts || DEFAULT_FALLBACK_PRODUCTS;
}

/**
 * Trích xuất tên sản phẩm từ lịch sử hội thoại trước đó để giữ ngữ cảnh (Context Retention)
 */
function extractProductFromHistory(history = []) {
  if (!history || !Array.isArray(history) || history.length === 0) return null;
  for (let i = history.length - 1; i >= 0; i--) {
    const content = (history[i].content || history[i].text || '').toLowerCase();
    const match = content.match(/đang xem sản phẩm ['"“”](.*?)['"“”]/i) || content.match(/sản phẩm ['"“”](.*?)['"“”]/i);
    if (match && match[1]) {
      return match[1].trim();
    }
    const productList = cachedProducts || DEFAULT_FALLBACK_PRODUCTS;
    for (const p of productList) {
      if (content.includes(p.ten_san_pham.toLowerCase())) {
        return p.ten_san_pham;
      }
    }
  }
  return null;
}

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
 * Hỗ trợ tham số history để giữ ngữ cảnh hội thoại
 */
export async function processOfflineRuleBased(message, history = []) {
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

  // 2. TƯ VẤN CHỌN SIZE THEO CHIỀU CAO & CÂN NẶNG (KÈM NGỮ CẢNH TỪ HISTORY)
  const { height, weight } = extractMeasurements(message);
  if (height || weight) {
    const h = height || 170;
    const w = weight || 65;
    const { size, advice } = calculateClothingSize(h, w);

    let reply = `Dạ, với vóc dáng chiều cao khoảng ${h}cm và cân nặng ${w}kg, Stylist AI khuyên bạn nên chọn Size ${size} ạ!\n${advice}\n\n`;

    // Kiểm tra xem khách có đang hỏi về một sản phẩm cụ thể từ trước không
    const previousProduct = extractProductFromHistory(history);
    if (previousProduct) {
      try {
        const variants = await getProductVariants(previousProduct);
        if (variants && Array.isArray(variants) && variants.length > 0) {
          const matchingVariants = variants.filter(v => (v.kich_co || '').toUpperCase() === size);
          if (matchingVariants.length > 0) {
            reply += `Với mẫu ${previousProduct} bạn đang quan tâm:\n`;
            matchingVariants.forEach(v => {
              const stock = v.so_luong_ton > 0 ? `Còn ${v.so_luong_ton} cái` : 'Tạm hết';
              reply += ` - Màu ${v.mau_sac} (Size ${size}): ${stock}\n`;
            });
            reply += `\nQuý khách muốn chọn màu nào để em hướng dẫn thêm vào giỏ hàng nhé ạ!`;
            return {
              reply: reply.trim(),
              toolsUsed: [{ name: 'RuleBased_ContextualSize', args: { product: previousProduct, size } }],
              engine: 'Rule-Based Offline'
            };
          }
        }
      } catch (e) {
        console.warn('[Offline Engine] Lỗi tra cứu size theo sản phẩm context:', e.message);
      }
    }

    // Nếu không có context sản phẩm, gợi ý top sản phẩm đang có sẵn size này trong kho
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
      reply: reply.trim(),
      toolsUsed: [{ name: 'RuleBased_SizeCalculator', args: { height: h, weight: w, size } }],
      engine: 'Rule-Based Offline'
    };
  }

  // 3. TƯ VẤN PHỐI ĐỒ / MIX & MATCH / ĐI CHƠI / CÔNG SỞ
  if (/(phối đồ|mix đồ|kết hợp|đi chơi|công sở|đi làm|đi tiệc|hẹn hò|mặc gì)/i.test(message)) {
    return {
      reply: "Dạ, Stylist AI gợi ý cho bạn các công thức phối đồ chuẩn phong cách QF NOIR ạ:\n\n" +
             "1. Đi làm công sở lịch lãm: Áo sơ mi Oxford hoặc Áo polo Pique kết hợp Quần tây Slimfit / Quần kaki Chinos, hoàn thiện cùng Thắt lưng da bò thật.\n" +
             "2. Dạo phố năng động: Áo thun Cotton Compact hoặc Áo thun Oversize phối với Quần short Cargo túi hộp hoặc Quần jean Slimfit co giãn.\n" +
             "3. Smart Casual thời thượng: Áo len cổ lọ dệt mịn bên trong khoác cùng Áo khoác Bomber gió cản nước và Quần âu xếp ly Ý.\n" +
             "4. Thể thao ngày hè: Set Đồ Bộ Thể Thao Nam Mùa Hè QF vải Dry-Ex co giãn 4 chiều thoáng khí.\n\n" +
             "Bạn đang chuẩn bị trang phục cho dịp nào để em tư vấn chi tiết hơn ạ?",
      toolsUsed: [{ name: 'RuleBased_MixAndMatch' }],
      engine: 'Rule-Based Offline'
    };
  }

  // 4. DANH MỤC CÁC LOẠI ÁO (POLO, SƠ MI, THUN, KHOÁC, HOODIE)
  if (/(loại áo|mẫu áo|dòng áo|danh mục áo|áo polo|áo thun|sơ mi|áo khoác|áo len|hoodie|sweater|\báo\b)/i.test(message) && !message.includes('quần')) {
    return {
      reply: "Dạ, hiện tại QF NOIR đang có sẵn các nhóm áo thời trang cao cấp sau để bạn lựa chọn ạ:\n\n" +
             "1. Áo Thun Nam: Thun Cotton Basic (187.000đ), Thun Cổ Tròn Premium (210.000đ), Thun Oversize Form Rộng (255.000đ), Thun Thể Thao Dry-Ex (195.000đ).\n" +
             "2. Áo Polo Nam: Áo Polo Thể Thao Nam QF Pique co giãn 4 chiều thoáng khí (219.000đ).\n" +
             "3. Áo Sơ Mi Nam: Sơ mi Oxford Dài Tay (256.000đ), Sơ mi Công Sở Slimfit chống nhăn (475.000đ), Sơ mi Linen Ngắn Tay (464.000đ).\n" +
             "4. Áo Khoác Nam: Áo Khoác Bomber Gió Nam (171.000đ), Áo Khoác Dù 2 Lớp Chống Nước (190.000đ).\n" +
             "5. Áo Len & Hoodie: Áo Hoodie Nỉ Bông (320.000đ), Áo Len Cổ Lọ Hàn Quốc (380.000đ).\n\n" +
             "Bạn đang quan tâm đến mẫu áo nào để em kiểm tra màu sắc và bảng size chi tiết ạ?",
      toolsUsed: [{ name: 'RuleBased_ShirtCategory' }],
      engine: 'Rule-Based Offline'
    };
  }

  // 5. DANH MỤC CÁC LOẠI QUẦN (JEAN, TÂY, KAKI, JOGGER, SHORT)
  if (/(loại quần|mẫu quần|dòng quần|danh mục quần|quần tây|quần jean|quần kaki|quần jogger|quần short|quần âu|\bquần\b)/i.test(message)) {
    return {
      reply: "Dạ, QF NOIR có đa dạng các dòng quần nam hiện đại chuẩn phong cách Minimalist ạ:\n\n" +
             "1. Quần Tây & Âu: Quần Tây Slimfit Co Giãn Công Sở (380.000đ), Quần Âu Xếp Ly Phong Cách Ý (420.000đ), Quần Tây Cạp Cao Gurkha (490.000đ).\n" +
             "2. Quần Jean Nam: Quần Jean Slimfit Co Giãn (549.000đ), Quần Jean Ống Suông Straight (480.000đ), Quần Jean Rách Gối Cá Tính (520.000đ).\n" +
             "3. Quần Kaki & Jogger: Quần Kaki Slimfit (340.000đ), Quần Kaki Ống Đứng Chinos (360.000đ), Quần Jogger Nỉ Bo Gấu (290.000đ).\n" +
             "4. Quần Short Nam: Quần Short Cargo Túi Hộp (302.000đ), Quần Short Kaki Đi Biển Dạo Phố (220.000đ).\n\n" +
             "Bạn đang tìm quần mặc đi làm công sở hay đi chơi dạo phố để em hỗ trợ nhé ạ?",
      toolsUsed: [{ name: 'RuleBased_PantsCategory' }],
      engine: 'Rule-Based Offline'
    };
  }

  // 6. BALO & PHỤ KIỆN (TÚI, VÍ, THẮT LƯNG)
  if (/(balo|túi|ví|thắt lưng|phụ kiện|clutch|tote)/i.test(message)) {
    return {
      reply: "Dạ, QF NOIR có các dòng phụ kiện thời trang nam cao cấp sau ạ:\n\n" +
             "- Balo Laptop Nam QF Chống Trộm vải Oxford kháng nước (390.000đ).\n" +
             "- Túi Đeo Chéo Nam Da PU Kháng Nước cao cấp (260.000đ).\n" +
             "- Túi Tote Canvas Nam QF Tối Giản (150.000đ).\n" +
             "- Thắt Lưng Da Bò Nam Khóa Kim sang trọng (280.000đ).\n" +
             "- Ví Da Nam Gấp Đôi Cao Cấp và Ví Cầm Tay Clutch Da Bò Thật.\n\n" +
             "Bạn đang quan tâm đến phụ kiện nào để em kiểm tra tình trạng kho hàng ạ?",
      toolsUsed: [{ name: 'RuleBased_AccessoriesCategory' }],
      engine: 'Rule-Based Offline'
    };
  }

  // 7. TRA CỨU KHUYẾN MÃI / VOUCHER
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
    } catch (e) {
      console.warn('[Offline Engine] Lỗi lấy khuyến mãi:', e.message);
    }
    return {
      reply: "Dạ, hiện tại shop có các ưu đãi hot:\n- Miễn phí vận chuyển toàn quốc cho đơn từ 500.000đ.\n- Mã giảm giá SALE50K giảm 50.000đ cho đơn từ 400.000đ.\n- Chiết khấu VIP 5-15% tự động áp dụng khi đăng nhập.\nBạn nhớ áp mã khi đặt hàng nhé ạ!",
      toolsUsed: [{ name: 'RuleBased_PromotionsFallback' }],
      engine: 'Rule-Based Offline'
    };
  }

  // 8. TRA CỨU SẢN PHẨM & TỒN KHO THEO TỪ KHÓA CỤ THỂ
  try {
    const products = await getCachedProducts();
    let matchedProduct = null;

    if (products && products.length > 0) {
      // 8.1. Ưu tiên khớp chính xác hoặc khớp từ khóa đặc trưng
      for (const p of products) {
        const pNameLower = p.ten_san_pham.toLowerCase();
        if (lowerMsg.includes(pNameLower)) {
          matchedProduct = p;
          break;
        }
      }

      // 8.2. Khớp từ khóa cụm (ít nhất 2 từ)
      if (!matchedProduct) {
        for (const p of products) {
          const pNameLower = p.ten_san_pham.toLowerCase();
          const words = pNameLower.split(' ').filter(w => w.length > 2);
          let matchCount = 0;
          words.forEach(w => {
            if (lowerMsg.includes(w)) matchCount++;
          });
          if (matchCount >= 2) {
            matchedProduct = p;
            break;
          }
        }
      }

      // 8.3. Khớp các từ khóa đặc trưng duy nhất (ví dụ: "bomber", "oxford", "dry-ex", "gurkha", "chinos", "clutch", "pique")
      if (!matchedProduct) {
        const uniqueKeywords = ['bomber', 'oxford', 'dry-ex', 'gurkha', 'chinos', 'clutch', 'pique', 'cargo', 'jogger'];
        for (const kw of uniqueKeywords) {
          if (lowerMsg.includes(kw)) {
            const found = products.find(p => p.ten_san_pham.toLowerCase().includes(kw));
            if (found) {
              matchedProduct = found;
              break;
            }
          }
        }
      }
    }

    if (matchedProduct) {
      const variants = await getProductVariants(matchedProduct.ten_san_pham);
      if (variants && Array.isArray(variants) && variants.length > 0) {
        let reply = `Dạ, mẫu ${matchedProduct.ten_san_pham} hiện đang có các biến thể trong kho như sau ạ:\n\n`;
        
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

  // 9. CÂU HỎI CHUNG / MẶC ĐỊNH
  return {
    reply: "Dạ, em là Stylist AI của QF NOIR! Em có thể giúp bạn:\n" +
           "- Tư vấn chọn size chuẩn: Bạn cho em biết chiều cao và cân nặng nhé.\n" +
           "- Gợi ý phối đồ thời trang: Bạn hỏi về cách mix đồ đi chơi, công sở, dạo phố.\n" +
           "- Danh mục & Tồn kho: Bạn hỏi về các mẫu áo, quần, balo, phụ kiện hoặc tên mẫu bạn đang xem.\n" +
           "- Mã giảm giá: Hỏi về các ưu đãi, voucher hôm nay.\n\n" +
           "Bạn đang quan tâm đến sản phẩm nào để em hỗ trợ tư vấn ngay ạ?",
    toolsUsed: [{ name: 'Default_Greeting' }],
    engine: 'Rule-Based Offline'
  };
}
