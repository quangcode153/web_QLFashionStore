import express from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import path from 'path';
import { fileURLToPath } from 'url';
import chatRoutes from './routes/chatRoutes.js';
import { getDbPool } from './config/db.js';

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.PORT || 3001;

// Middlewares
app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, '../public')));

// Routes
app.use('/api', chatRoutes);

// Middleware xử lý lỗi tập trung (ngăn chặn crash khi nhận JSON lỗi hoặc request bất thường)
app.use((err, req, res, next) => {
  if (err instanceof SyntaxError && err.status === 400 && 'body' in err) {
    return res.status(400).json({ error: 'Dữ liệu JSON không hợp lệ.' });
  }
  console.error('[Server Error]', err);
  return res.status(500).json({ error: 'Lỗi máy chủ nội bộ.', details: err.message });
});

// Bảo vệ tiến trình chống crash do lỗi không mong muốn
process.on('uncaughtException', (err) => {
  console.error('[Uncaught Exception]', err);
});

process.on('unhandledRejection', (reason) => {
  console.error('[Unhandled Rejection]', reason);
});

// Khởi động server
app.listen(PORT, async () => {
  console.log('====================================================');
  console.log(`🤖 Fashion Store AI Chatbot Server đang chạy tại:`);
  console.log(`👉 Local URL: http://localhost:${PORT}`);
  console.log(`👉 Health check: http://localhost:${PORT}/api/health`);
  console.log('====================================================');

  // Thử kết nối SQL Server lúc khởi động
  try {
    await getDbPool();
  } catch (err) {
    console.warn(`[LƯU Ý] Chưa kết nối được SQL Server ngay lúc này. Chatbot vẫn chạy và sẽ thử lại khi có yêu cầu.`);
  }
});
