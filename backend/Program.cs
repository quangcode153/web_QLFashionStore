using System.Text;
using Backend.Middlewares;
using Backend.Repositories;
using Backend.Security;
using Backend.Services;
using Backend.WebSockets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Load biến môi trường từ .env nếu có ở thư mục gốc hoặc thư mục hiện tại
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Cấu hình Swagger với hỗ trợ Bearer Token (JWT)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Fashion Store Management & E-Commerce API", 
        Version = "v1",
        Description = "Hệ thống REST API cho chuỗi cửa hàng thời trang FashionStore"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập 'Bearer {token}' để xác thực",
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

// 3. Cấu hình CORS cho phép kết nối từ Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 4. Cấu hình SignalR (WebSockets)
builder.Services.AddSignalR();

// 5. Cấu hình Xác thực JWT Token
var jwtSecretKey = builder.Configuration["JWT_SECRET_KEY"] 
    ?? builder.Configuration["Jwt:Key"] 
    ?? "FashionStore_Secret_Key_Super_Secure_2026_JWT_Token_AtLeast_32Characters!";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"] ?? "FashionStoreBackend";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"] ?? "FashionStoreClients";

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

// 6. Đăng ký Dependency Injection (IoC Container)
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

var app = builder.Build();

// 7. Cấu hình HTTP Request Pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fashion Store API v1");
        c.RoutePrefix = string.Empty; // Mở Swagger trực tiếp tại URL gốc (http://localhost:5000/)
    });
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hubs (WebSockets)
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<InventoryHub>("/hubs/inventory");

app.Run();
