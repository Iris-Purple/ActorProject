using AuthServer.Services;

var builder = WebApplication.CreateBuilder(args);

// === 서비스 등록 ===
builder.Services.AddControllers();

// AccountDatabase를 싱글톤으로 등록 - 추가
builder.Services.AddSingleton<AccountDatabase>();

// CORS 설정 - 클라이언트 접근 허용
builder.Services.AddCors(options =>
{
    options.AddPolicy("GameClient",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("GameClient");  // 추가: CORS 적용
app.UseAuthorization();
app.MapControllers();

// 시작 로그
app.Logger.LogInformation("AuthServer starting on port {Port}", 
    app.Configuration["Urls"] ?? "https://localhost:7000");

app.Run();

public partial class Program { }