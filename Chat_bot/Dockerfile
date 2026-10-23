# Sử dụng base image Node.js Alpine siêu nhẹ (~80MB)
FROM node:20-alpine

# Thư mục làm việc trong container
WORKDIR /app

# Sao chép file cấu hình package
COPY package*.json ./

# Cài đặt dependency production
RUN npm ci --only=production

# Sao chép toàn bộ mã nguồn
COPY . .

# Mở cổng 3001
EXPOSE 3001

# Lệnh khởi động server
CMD ["node", "src/server.js"]
