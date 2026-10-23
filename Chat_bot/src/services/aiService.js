import dotenv from 'dotenv';
import {
  searchProducts,
  getProductVariants,
  checkStock,
  getActivePromotions,
} from './inventoryService.js';
import { processOfflineRuleBased } from './ruleBasedService.js';

dotenv.config();

const SYSTEM_PROMPT = `
Bạn là "Stylist AI" - Trợ lý tư vấn thời trang thông minh và chuyên nghiệp của thương hiệu QF NOIR.

=== NGUYÊN TẮC GIỚI HẠN PHẠM VI (GUARDRAILS) - BẮT BUỘC TUÂN THỦ ===
1. PHẠM VI ĐƯỢC PHÉP:
   - Tư vấn các mẫu sản phẩm thời trang (áo thun, polo, set đồ bộ thể thao, quần jogger, short, balo...).
   - Tư vấn chọn size dựa theo chiều cao và cân nặng của khách (kèm giải thích form dáng).
   - Kiểm tra màu sắc, các biến thể và số lượng TỒN KHO THỰC TẾ.
   - Báo giá bán và thông tin các chương trình khuyến mãi / mã giảm giá voucher.

2. NGOÀI PHẠM VI (TỪ CHỐI LỊCH SỰ):
   - Tuyệt đối từ chối các câu hỏi không liên quan đến thời trang và sản phẩm của shop (toán học, lập trình code, chính trị, dịch thuật ngoài lề, chuyện phiếm...).
   - Mẫu từ chối: "Dạ, em là Stylist AI của QF NOIR. Em chỉ có thể hỗ trợ quý khách thông tin về các mẫu thời trang, màu sắc, size và tình trạng còn hàng trong kho thôi ạ. Quý khách đang quan tâm đến sản phẩm nào để em tư vấn ngay ạ?"

=== NGUYÊN TẮC TỒN KHO & CÔNG CỤ (TOOLS) ===
- Khi khách hỏi về số lượng còn hàng, các màu sắc hay giá: BẮT BUỘC sử dụng các Tools được cung cấp (searchProducts, getProductVariants, checkStock, getActivePromotions).
- TUYỆT ĐỐI KHÔNG TỰ BỊA SỐ LƯỢNG TỒN HOẶC MÀU SẮC.
- DỮ LIỆU TỒN KHO: Dữ liệu tồn kho do công cụ trả về là số lượng thực tế tại KHO ONLINE (Kho Tân Bình) của shop để phục vụ giao hàng tận nơi. Hãy thông báo số lượng này cho khách an tâm đặt hàng trực tuyến. Nếu khách muốn ghé mua trực tiếp tại cửa hàng offline gần nhà, hãy hướng dẫn khách bấm nút 'Tra cứu tồn kho các chi nhánh' ngay trên trang chi tiết sản phẩm của web.
- Giọng văn: Lịch sự, chu đáo, thân thiện, dạ/thưa chuẩn phong cách Stylist thời trang cao cấp.

=== QUY TẮC ĐỊNH DẠNG VĂN BẢN (BẮT BUỘC) ===
- TUYỆT ĐỐI KHÔNG sử dụng ký tự in đậm hai dấu sao (cấm dùng **chữ**). Hãy viết văn bản thuần túy, sạch sẽ.
- TUYỆT ĐỐI KHÔNG sử dụng bất kỳ icon, emoji hay biểu tượng nào (cấm dùng các icon như 🎨, 📏, 📦, 🏷️, 😊, ✨, 👋...).
`;

// Định nghĩa Tools cho Groq (chuẩn OpenAI function calling)
const GROQ_TOOLS = [
  {
    type: 'function',
    function: {
      name: 'searchProducts',
      description: 'Tìm kiếm sản phẩm theo từ khóa (tên sản phẩm, danh mục, chất liệu).',
      parameters: {
        type: 'object',
        properties: {
          keyword: { type: 'string', description: 'Từ khóa tìm kiếm (áo thun, polo, set đồ bộ...)' },
        },
        required: ['keyword'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'getProductVariants',
      description: 'Lấy tất cả biến thể màu sắc, kích cỡ, giá bán và số lượng tồn kho của một sản phẩm.',
      parameters: {
        type: 'object',
        properties: {
          productName: { type: 'string', description: 'Tên sản phẩm' },
        },
        required: ['productName'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'checkStock',
      description: 'Kiểm tra chính xác tồn kho theo tên sản phẩm, màu sắc và/hoặc kích cỡ.',
      parameters: {
        type: 'object',
        properties: {
          productName: { type: 'string', description: 'Tên sản phẩm' },
          color: { type: 'string', description: 'Màu sắc (Be, Trắng, Đen...)' },
          size: { type: 'string', description: 'Kích cỡ (S, M, L, XL...)' },
        },
        required: ['productName'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'getActivePromotions',
      description: 'Lấy danh sách mã giảm giá và khuyến mãi đang có hiệu lực.',
      parameters: { type: 'object', properties: {} },
    },
  },
];

const availableFunctions = {
  searchProducts: async (args) => await searchProducts(args.keyword),
  getProductVariants: async (args) => await getProductVariants(args.productName),
  checkStock: async (args) => await checkStock(args),
  getActivePromotions: async () => await getActivePromotions(),
};

/**
 * Gọi Groq Cloud API (Tầng 1 - Online)
 */
async function callGroqChat(history, userMessage) {
  const apiKey = process.env.GROQ_API_KEY;
  if (!apiKey || apiKey.trim() === '') {
    throw new Error('Chưa cấu hình GROQ_API_KEY');
  }

  const model = process.env.GROQ_MODEL || 'qwen/qwen3.8-27b';

  const messages = [
    { role: 'system', content: SYSTEM_PROMPT },
    ...history.map(item => ({
      role: item.role === 'user' ? 'user' : 'assistant',
      content: item.content || item.text,
    })),
    { role: 'user', content: userMessage },
  ];

  const toolsUsed = [];
  let currentMessages = [...messages];

  // Vòng lặp Function Calling (tối đa 4 vòng lặp an toàn)
  for (let step = 0; step < 4; step++) {
    const res = await fetch('https://api.groq.com/openai/v1/chat/completions', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        model: model,
        messages: currentMessages,
        tools: GROQ_TOOLS,
        tool_choice: 'auto',
        temperature: 0.3,
        max_tokens: 600,
      }),
      signal: AbortSignal.timeout(12000), // Timeout 12 giây
    });

    if (!res.ok) {
      const errBody = await res.text();
      throw new Error(`Groq API Error ${res.status}: ${errBody}`);
    }

    const data = await res.json();
    const choice = data.choices[0];
    const message = choice.message;

    // Nếu AI yêu cầu gọi Tool
    if (message.tool_calls && message.tool_calls.length > 0) {
      currentMessages.push(message);

      for (const toolCall of message.tool_calls) {
        const funcName = toolCall.function.name;
        let funcArgs = {};
        try {
          funcArgs = JSON.parse(toolCall.function.arguments || '{}');
        } catch (e) {}

        toolsUsed.push({ name: funcName, args: funcArgs });
        console.log(`[Groq AI Tool Call] Đang thực thi: ${funcName}`, funcArgs);

        let funcResult = { error: 'Tool không tồn tại' };
        if (availableFunctions[funcName]) {
          funcResult = await availableFunctions[funcName](funcArgs);
        }

        currentMessages.push({
          role: 'tool',
          tool_call_id: toolCall.id,
          content: JSON.stringify(funcResult),
        });
      }
    } else {
      // Đã có câu trả lời hoàn chỉnh
      return {
        reply: message.content || '',
        toolsUsed,
        engine: 'Groq Cloud (Online)',
      };
    }
  }

  throw new Error('Đã vượt quá số lần gọi tool');
}

/**
 * HÀM CHÍNH XỬ LÝ CHAT - KIẾN TRÚC 2 LỚP:
 * - Tầng 1: Groq Cloud API (Siêu tốc, thông minh, 14.400 req/ngày miễn phí)
 * - Tầng 2: Rule-Based Engine (Offline, 0đ, không cần mạng, vô hạn quota)
 */
export async function processChatMessage(history = [], userMessage) {
  // THỬ TẦNG 1: GROQ CLOUD AI
  try {
    const result = await callGroqChat(history, userMessage);
    return result;
  } catch (groqError) {
    console.warn(`[Tầng 1 Groq Error]: ${groqError.message}. TỰ ĐỘNG CHUYỂN SANG TẦNG 2: Rule-Based Offline Engine...`);

    // TẦNG 2: OFFLINE RULE-BASED ENGINE DỰ PHÒNG
    try {
      const offlineResult = await processOfflineRuleBased(userMessage);
      return offlineResult;
    } catch (fallbackError) {
      console.error('[Tầng 2 Error]:', fallbackError);
      return {
        reply: "Dạ, hiện tại hệ thống Stylist AI đang cập nhật kho hàng trong giây lát. Quý khách vui lòng thử lại sau ít giây ạ!",
        toolsUsed: [],
        engine: 'Fallback Error Handler',
      };
    }
  }
}
