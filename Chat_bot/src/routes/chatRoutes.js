import express from 'express';
import { processChatMessage } from '../services/aiService.js';
import {
  searchProducts,
  getProductVariants,
  checkStock,
  getActivePromotions,
} from '../services/inventoryService.js';
import { getDbPool, executeQuery } from '../config/db.js';

const router = express.Router();

/**
 * Endpoint chính: Gửi tin nhắn đến AI Chatbot
 */
router.post('/chat', async (req, res) => {
  try {
    const { message, history } = req.body;
    if (!message || message.trim() === '') {
      return res.status(400).json({ error: 'Nội dung tin nhắn không được để trống.' });
    }

    const result = await processChatMessage(history || [], message);
    return res.json(result);
  } catch (error) {
    console.error('[API /chat] Lỗi:', error);
    return res.status(500).json({
      error: 'Đã xảy ra lỗi khi xử lý tin nhắn.',
      details: error.message,
    });
  }
});

/**
 * Endpoint kiểm tra trạng thái sức khỏe kết nối CSDL SQL Server
 */
router.get('/health', async (req, res) => {
  const hasGeminiKey = Boolean(process.env.GEMINI_API_KEY && process.env.GEMINI_API_KEY !== 'your_gemini_api_key_here');
  try {
    const rows = await executeQuery('SELECT DB_NAME() AS currentDb, GETDATE() AS serverTime, COUNT(*) AS totalTables FROM sys.tables');
    if (rows && rows.length > 0) {
      return res.json({
        status: 'OK',
        database: rows[0].currentDb,
        serverTime: rows[0].serverTime,
        totalTables: rows[0].totalTables,
        hasGeminiKey,
      });
    }
    return res.json({ status: 'OK', hasGeminiKey });
  } catch (error) {
    return res.json({
      status: 'DB_DISCONNECTED',
      message: error.message,
      hasGeminiKey,
    });
  }
});

/**
 * Endpoint test trực tiếp dữ liệu tồn kho (không qua AI)
 */
router.get('/test-stock', async (req, res) => {
  try {
    const { product = '', color = '', size = '' } = req.query;
    const data = await checkStock({ productName: product, color, size });
    return res.json(data);
  } catch (error) {
    return res.status(500).json({ error: error.message });
  }
});

export default router;
