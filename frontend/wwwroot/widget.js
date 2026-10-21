(function () {
  // Tránh nhúng 2 lần
  if (document.getElementById('qf-chat-widget-root')) return;

  // Xác định URL của Chatbot API server (Hỗ trợ cấu hình window.QF_CHATBOT_URL)
  let backendUrl = window.QF_CHATBOT_URL || 'http://localhost:3001';
  const scriptTag = document.currentScript;
  if (!window.QF_CHATBOT_URL && scriptTag && scriptTag.src) {
    try {
      const url = new URL(scriptTag.src);
      if (url.port === '3001') {
        backendUrl = `${url.protocol}//${url.host}`;
      }
    } catch (e) {}
  }

  // Tự động nhúng widget.css nếu chưa có
  if (!document.getElementById('qf-widget-style')) {
    const link = document.createElement('link');
    link.id = 'qf-widget-style';
    link.rel = 'stylesheet';
    link.href = 'widget.css';
    document.head.appendChild(link);
  }

  // Tạo phần tử Root
  const root = document.createElement('div');
  root.id = 'qf-chat-widget-root';
  document.body.appendChild(root);

  // Template HTML Widget (Thiết kế tối giản sang trọng, không emoji icon, không **)
  root.innerHTML = `
    <!-- Cửa sổ Chat -->
    <div class="qf-chat-window" id="qfChatWindow">
      <div class="qf-chat-header">
        <div class="qf-header-brand">
          <div class="qf-header-avatar">QF</div>
          <div class="qf-header-info">
            <h3>Stylist AI Tư Vấn <span class="qf-online-dot"></span></h3>
            <p>Hỗ trợ chọn size và kiểm tra tồn kho</p>
          </div>
        </div>
        <button class="qf-header-close-btn" id="qfCloseBtn" title="Thu nhỏ">✕</button>
      </div>

      <div class="qf-chat-body" id="qfChatBody">
        <div class="qf-msg-row bot">
          <div class="qf-msg-avatar">AI</div>
          <div class="qf-msg-bubble">
            Chào bạn. Mình là Stylist AI của QF NOIR.<br><br>
            Bạn cao và nặng bao nhiêu kg? Mình sẽ giúp bạn tìm size vừa vặn nhất, hoặc bạn có thể hỏi mình về màu sắc và số lượng còn hàng trong kho của bất kỳ mẫu nào nhé.
            
            <div class="qf-quick-actions">
              <button class="qf-quick-btn" onclick="window.qfSendQuick('Mình cao 1m72 nặng 65kg nên mặc size gì?')">Tư vấn chọn size</button>
              <button class="qf-quick-btn" onclick="window.qfSendQuick('Set Đồ Bộ Thể Thao Nam Mùa Hè QF còn màu gì và size nào?')">Màu và tồn kho Set Thể Thao</button>
              <button class="qf-quick-btn" onclick="window.qfSendQuick('Shop hiện có mã giảm giá voucher nào không?')">Mã giảm giá hôm nay</button>
            </div>
          </div>
        </div>
      </div>

      <div class="qf-chat-footer">
        <input 
          type="text" 
          id="qfInputField" 
          class="qf-input-field" 
          placeholder="Nhập chiều cao, cân nặng hoặc câu hỏi..." 
          autocomplete="off"
        />
        <button id="qfSendBtn" class="qf-send-btn" title="Gửi tin">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="22" y1="2" x2="11" y2="13"></line>
            <polygon points="22 2 15 22 11 13 2 9 22 2"></polygon>
          </svg>
        </button>
      </div>
    </div>

    <!-- Nút kích hoạt nổi (Thay thế / nâng cấp cho nút Hỗ trợ chọn size) -->
    <div class="qf-launcher-btn" id="qfLauncherBtn" title="Mở Stylist AI tư vấn size & tồn kho">
      <div class="qf-launcher-icon">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
        </svg>
      </div>
      <div class="qf-launcher-text">
        <span class="qf-launcher-title">Hỗ trợ chọn size</span>
        <span class="qf-launcher-sub">Stylist AI tư vấn</span>
      </div>
    </div>
  `;

  // DOM Elements
  const launcherBtn = document.getElementById('qfLauncherBtn');
  const chatWindow = document.getElementById('qfChatWindow');
  const closeBtn = document.getElementById('qfCloseBtn');
  const chatBody = document.getElementById('qfChatBody');
  const inputField = document.getElementById('qfInputField');
  const sendBtn = document.getElementById('qfSendBtn');

  let history = [];

  // Toggle Mở/Đóng cửa sổ chat
  function toggleChat() {
    chatWindow.classList.toggle('open');
    if (chatWindow.classList.contains('open')) {
      inputField.focus();
    }
  }

  function openChat() {
    chatWindow.classList.add('open');
    inputField.focus();
  }

  function closeChat() {
    chatWindow.classList.remove('open');
  }

  launcherBtn.addEventListener('click', toggleChat);
  closeBtn.addEventListener('click', closeChat);

  // Bộ lọc làm sạch văn bản: Loại bỏ toàn bộ dấu ** và toàn bộ biểu tượng icon / emoji
  function sanitizeCleanText(raw) {
    if (!raw) return '';
    // 1. Xóa toàn bộ dấu **
    let s = raw.replace(/\*\*/g, '');
    // 2. Xóa toàn bộ icon emoji Unicode
    s = s.replace(/[\u{1F300}-\u{1FAFF}\u{1F000}-\u{1F02F}\u{1F0A0}-\u{1F0FF}\u{1F100}-\u{1F64F}\u{1F680}-\u{1F6FF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}\u{FE00}-\u{FE0F}\u{1F900}-\u{1F9FF}✦✨👋😊👔📏🎨🏷️📦]/gu, '');
    // 3. Chuẩn hóa khoảng trống thừa ở đầu dòng
    s = s.replace(/^[ \t]+/gm, '');
    return s.replace(/\n/g, '<br>');
  }

  // Thêm tin nhắn vào giao diện
  function appendMessage(role, text, toolsUsed) {
    const row = document.createElement('div');
    row.className = `qf-msg-row ${role}`;

    const avatar = document.createElement('div');
    avatar.className = 'qf-msg-avatar';
    avatar.textContent = role === 'user' ? 'Tôi' : 'AI';

    const bubble = document.createElement('div');
    bubble.className = 'qf-msg-bubble';
    bubble.innerHTML = sanitizeCleanText(text);

    row.appendChild(avatar);
    row.appendChild(bubble);
    chatBody.appendChild(row);
    chatBody.scrollTop = chatBody.scrollHeight;
  }

  // Hiển thị trạng thái đang soạn tin (typing)
  function showTyping() {
    const row = document.createElement('div');
    row.className = 'qf-msg-row bot';
    row.id = 'qfTypingRow';

    const avatar = document.createElement('div');
    avatar.className = 'qf-msg-avatar';
    avatar.textContent = 'AI';

    const bubble = document.createElement('div');
    bubble.className = 'qf-msg-bubble';
    bubble.innerHTML = '<span class="qf-typing"><span></span><span></span><span></span></span>';

    row.appendChild(avatar);
    row.appendChild(bubble);
    chatBody.appendChild(row);
    chatBody.scrollTop = chatBody.scrollHeight;
  }

  function removeTyping() {
    const typing = document.getElementById('qfTypingRow');
    if (typing) typing.remove();
  }

  // Gửi tin nhắn
  async function sendMessage(text) {
    const msg = (text || inputField.value).trim();
    if (!msg) return;

    appendMessage('user', msg);
    inputField.value = '';
    sendBtn.disabled = true;

    showTyping();

    try {
      const res = await fetch(`${backendUrl}/api/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: msg,
          history: history
        })
      });

      const data = await res.json();
      removeTyping();

      if (data.error) {
        appendMessage('bot', `Lỗi: ${data.details || data.error}`);
      } else {
        appendMessage('bot', data.reply, data.toolsUsed);
        history.push({ role: 'user', content: msg });
        history.push({ role: 'assistant', content: data.reply });
      }
    } catch (err) {
      removeTyping();
      appendMessage('bot', `Lỗi kết nối tới Stylist AI: ${err.message}`);
    } finally {
      sendBtn.disabled = false;
      inputField.focus();
    }
  }

  sendBtn.addEventListener('click', () => sendMessage());
  inputField.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') sendMessage();
  });

  // Expose hàm bấm chip gợi ý nhanh toàn cục
  window.qfSendQuick = function (promptText) {
    sendMessage(promptText);
  };

  // Expose các hàm điều khiển toàn cục cho Blazor C# gọi qua JSRuntime
  window.qfOpenChat = function (initialPrompt) {
    root.style.display = '';
    openChat();
    if (initialPrompt && typeof initialPrompt === 'string' && initialPrompt.trim() !== '') {
      sendMessage(initialPrompt);
    }
  };

  window.qfToggleChat = function () {
    toggleChat();
  };

  window.qfCloseChat = function () {
    closeChat();
  };

  // ============================================================
  // QUẢN LÝ HIỂN THỊ CHỈ TẠI TRANG CHỦ (HOME: / HOẶC /catalog)
  // ============================================================
  function isHomePage() {
    const pathname = window.location.pathname.toLowerCase().replace(/\/+$/, '') || '/';
    return pathname === '/' || pathname === '/catalog' || pathname === '/index.html';
  }

  function updateVisibilityByRoute() {
    if (isHomePage()) {
      if (root.style.display === 'none') {
        root.style.display = '';
      }
    } else {
      if (root.style.display !== 'none') {
        root.style.display = 'none';
        if (chatWindow && chatWindow.classList.contains('active')) {
          chatWindow.classList.remove('active');
        }
      }
    }
  }

  // Kiểm tra ngay khi tải widget
  updateVisibilityByRoute();

  // Bắt các sự kiện chuyển trang trong SPA (Blazor NavigationManager)
  window.addEventListener('popstate', updateVisibilityByRoute);

  const _origPushState = history.pushState;
  history.pushState = function () {
    const res = _origPushState.apply(this, arguments);
    setTimeout(updateVisibilityByRoute, 50);
    return res;
  };

  const _origReplaceState = history.replaceState;
  history.replaceState = function () {
    const res = _origReplaceState.apply(this, arguments);
    setTimeout(updateVisibilityByRoute, 50);
    return res;
  };

  // Vòng lặp đồng bộ định kỳ chống lệch trạng thái trong Blazor SPA
  setInterval(updateVisibilityByRoute, 300);
})();

