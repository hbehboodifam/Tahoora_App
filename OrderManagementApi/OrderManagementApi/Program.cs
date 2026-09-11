using Microsoft.EntityFrameworkCore;
using OrderManagementApi;
using OrderManagementApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== تشخیص خودکار نوع دیتابیس (MySQL یا PostgreSQL) =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Connection string 'DefaultConnection' not found.");
}

if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
    connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
    connectionString.Contains("render.com", StringComparison.OrdinalIgnoreCase))
{
    // ===== PostgreSQL (برای Render) =====
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // ===== MySQL (برای Local) =====
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}

// ===== سرویس‌ها =====
builder.Services.AddScoped<SmsService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// ===== اجرای Migration فقط در محیط Local =====
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Migration Error: {ex.Message}");
        }
    }
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();