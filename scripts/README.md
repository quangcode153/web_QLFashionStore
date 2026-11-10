# THƯ MỤC KỊCH BẢN KHỞI CHẠY (SCRIPTS DIRECTORY)

Thư mục này chứa các kịch bản chạy bổ sung, phục vụ phát triển nội bộ và lưu trữ lịch sử:

### 1. Kịch bản chạy chính (ở thư mục gốc dự án):
- **`Bat_He_Thong.bat`** (Thư mục gốc): Khởi động toàn diện hệ thống phục vụ website Online Public Vercel (Kiểm tra SQL Server -> Chạy Chatbot 3001 -> Chạy Backend 5000 -> Mở Ngrok Tunnel cố định).
- **`Tat_He_Thong.bat`** (Thư mục gốc): Tắt toàn bộ hệ thống, giải phóng hoàn toàn các cổng mạng 3000, 3001, 5000 và tiến trình ngrok.

---

### 2. Kịch bản chạy Local (trong thư mục `scripts/`):
- **`Chay_Du_An_Local.bat`**: Khởi động cả 3 dịch vụ cục bộ trên máy tính:
  - Backend API: `http://localhost:5000`
  - Frontend Blazor Wasm: `http://localhost:3000`
  - Stylist AI Chatbot: `http://localhost:3001`
- **`run_local_browser.bat`**: Tương tự `Chay_Du_An_Local.bat` nhưng tự động mở trình duyệt web đến `http://localhost:3000`.

---

### 3. Thư mục lưu trữ (`scripts/archive/`):
- **`Chay_He_Thong_Public.bat`**: Bản lưu trữ trùng lặp trước đó của `Bat_He_Thong.bat`.

