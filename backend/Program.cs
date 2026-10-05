using System.Text;
using System.Threading.RateLimiting;
using Backend.Database;
using Backend.Middlewares;
using Backend.Repositories;
using Backend.Security;
using Backend.Services;
using Backend.WebSockets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Shared.DTOs.Common;

// 1. Tự động ánh xạ snake_case trong SQL Server sang PascalCase trong C# DTOs
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// 2. Load biến môi trường từ .env
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// 3. Đăng ký Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 4. Cấu hình Swagger với hỗ trợ Bearer Token (JWT)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Fashion Store Management & E-Commerce API", 
        Version = "v1",
        Description = "Hệ thống REST API cho chuỗi cửa hàng thời trang FashionStore (Phân quyền 4 Roles: Admin chuỗi, Quản lý chi nhánh, Nhân viên bán hàng, Khách hàng)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập token JWT theo định dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 5. Cấu hình CORS chặt chẽ giữa Backend (Port 5000) và Frontend (Port 3000)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 6. Cấu hình SignalR (WebSockets)
builder.Services.AddSignalR();

// 7. Cấu hình Xác thực JWT Token (Fail-Fast: Không fallback key mặc định)
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? builder.Configuration["JWT_SECRET_KEY"] 
    ?? builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
{
    throw new InvalidOperationException(
        "LỖI BẢO MẬT NGHIÊM TRỌNG (Fail-Fast): Khóa bí mật JWT chưa được cấu hình hoặc quá ngắn (< 32 ký tự). " +
        "Bắt buộc thiết lập biến môi trường 'JWT_SECRET_KEY' trong file .env hoặc cấu hình server.");
}

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
    ?? builder.Configuration["JWT_ISSUER"] 
    ?? builder.Configuration["Jwt:Issuer"] 
    ?? "FashionStoreBackend";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
    ?? builder.Configuration["JWT_AUDIENCE"] 
    ?? builder.Configuration["Jwt:Audience"] 
    ?? "FashionStoreClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
});

// 8. Đăng ký Dependency Injection (IoC Container)
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// Repositories
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();

// 9. Cấu hình Rate Limiting (Chống Brute-Force & DoS)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var response = ApiResponse<string>.Fail(
            "Bạn đã gửi quá nhiều yêu cầu đăng nhập. Vui lòng thử lại sau 1 phút (Chống tấn công Brute-force).");
        await context.HttpContext.Response.WriteAsJsonAsync(response, token);
    };

    options.AddPolicy("LoginRateLimit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// 10. Tự động sinh bảng và cấu trúc CSDL bằng code khi khởi động
var connStr = app.Configuration["CONNECTION_STRING"] 
    ?? app.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("CẤU HÌNH CSDL: Chưa thiết lập chuỗi kết nối cơ sở dữ liệu. Vui lòng thiết lập CONNECTION_STRING trong file .env hoặc biến môi trường.");

DatabaseInitializer.Initialize(connStr, app.Logger);

// 11. Cấu hình HTTP Request Pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fashion Store API v1");
    c.RoutePrefix = string.Empty; // Mở Swagger trực tiếp tại URL gốc (http://localhost:5000/)
});

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowFrontend");
app.UseRateLimiter(); // Middleware kiểm soát tần suất gọi API

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hubs (WebSockets)
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<InventoryHub>("/hubs/inventory");

app.Run();
