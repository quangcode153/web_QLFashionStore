import sql from 'mssql';
import dotenv from 'dotenv';
import { execFile } from 'child_process';
import util from 'util';
import path from 'path';
import { fileURLToPath } from 'url';

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const execFileAsync = util.promisify(execFile);

const config = {
  user: process.env.DB_USER || 'sa',
  password: process.env.DB_PASSWORD || '',
  server: process.env.DB_SERVER || 'localhost',
  port: parseInt(process.env.DB_PORT || '1433', 10),
  database: process.env.DB_NAME || 'FashionStore',
  options: {
    encrypt: process.env.DB_ENCRYPT === 'true',
    trustServerCertificate: process.env.DB_TRUST_SERVER_CERTIFICATE !== 'false',
    enableArithAbort: true,
    connectTimeout: 4000,
  },
  pool: {
    max: 10,
    min: 0,
    idleTimeoutMillis: 30000,
  },
};

let poolPromise = null;
let useFallbackRunner = false;

/**
 * Lấy pool kết nối MSSQL (TCP/IP cho Docker / Production)
 */
export async function getDbPool() {
  if (useFallbackRunner) return null;

  if (!poolPromise) {
    poolPromise = sql.connect(config)
      .then(pool => {
        console.log(`[SQL Server] ✅ Đã kết nối TCP thành công: ${config.database} tại ${config.server}:${config.port}`);
        return pool;
      })
      .catch(err => {
        console.warn(`[SQL Server] TCP/IP chưa mở trên cổng ${config.port}. Kích hoạt chế độ Native ADO.NET Runner...`);
        poolPromise = null;
        useFallbackRunner = true;
        return null;
      });
  }
  return poolPromise;
}

/**
 * Thực thi câu lệnh SQL với cơ chế Dual-Mode:
 * Ưu tiên: Pool TCP/IP (khi chạy Docker / Production)
 * Dự phòng: Native ADO.NET Runner (khi chạy local SQLEXPRESS trên Windows)
 */
export async function executeQuery(queryText, inputs = {}) {
  // 1. Thử qua mssql Pool TCP nếu khả dụng
  if (!useFallbackRunner) {
    try {
      const pool = await getDbPool();
      if (pool) {
        const request = pool.request();
        for (const [key, value] of Object.entries(inputs)) {
          request.input(key, value);
        }
        const result = await request.query(queryText);
        return result.recordset || [];
      }
    } catch (err) {
      console.warn('[SQL Pool] Thất bại, chuyển sang Native Runner:', err.message);
      useFallbackRunner = true;
    }
  }

  // 2. Chế độ dự phòng: Thực thi qua queryRunner.ps1 (ADO.NET)
  try {
    let finalQuery = queryText;
    for (const [key, value] of Object.entries(inputs)) {
      const safeVal = String(value).replace(/'/g, "''");
      const regex = new RegExp(`@${key}\\b`, 'g');
      finalQuery = finalQuery.replace(regex, `N'${safeVal}'`);
    }

    const scriptPath = path.join(__dirname, 'queryRunner.ps1');
    const serverInstance = process.env.DB_SERVER === 'localhost' ? '.\\SQLEXPRESS' : process.env.DB_SERVER;
    const dbName = process.env.DB_NAME || 'FashionStore';
    const dbUser = process.env.DB_USER || 'sa';
    const dbPass = process.env.DB_PASSWORD || '';

    const args = [
      '-NoProfile',
      '-ExecutionPolicy', 'Bypass',
      '-File', scriptPath,
      '-Server', serverInstance,
      '-Database', dbName,
      '-User', dbUser,
      '-Password', dbPass,
      '-Query', finalQuery
    ];

    const { stdout } = await execFileAsync('powershell', args, {
      maxBuffer: 20 * 1024 * 1024,
      encoding: 'utf8'
    });

    const output = stdout.trim();
    if (!output || output === '[]') {
      return [];
    }

    return JSON.parse(output);
  } catch (error) {
    console.error('[Native Runner] Lỗi thực thi:', error.message);
    throw error;
  }
}

export { sql };
