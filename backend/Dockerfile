# ==============================================================================
# STAGE 1: Build & Publish Backend API (.NET 8 SDK)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Copy file csproj trước để tối ưu hóa Docker Layer Cache khi restore
COPY ["shared/Shared.csproj", "shared/"]
COPY ["backend/Backend.csproj", "backend/"]

# 2. Restore các NuGet packages
RUN dotnet restore "backend/Backend.csproj"

# 3. Copy toàn bộ mã nguồn của shared và backend
COPY shared/ shared/
COPY backend/ backend/

# 4. Biên dịch và publish ở chế độ Release
WORKDIR "/src/backend"
RUN dotnet publish "Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==============================================================================
# STAGE 2: Image Runtime ASP.NET Core 8.0 (Debian-based ổn định cao)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Thiết lập múi giờ Việt Nam và hỗ trợ Unicode tiếng Việt
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV TZ=Asia/Ho_Chi_Minh

# Cài đặt curl để hỗ trợ container healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Cấu hình cổng Kestrel chạy cố định tại port 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_HTTP_PORTS=5000
EXPOSE 5000

# Copy các file nhị phân đã build từ Stage 1
COPY --from=build /app/publish .

# Healthcheck kiểm tra trạng thái Backend
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:5000/ || exit 1

ENTRYPOINT ["dotnet", "Backend.dll"]
