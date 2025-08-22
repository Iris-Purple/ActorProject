using Common.Database;

var builder = WebApplication.CreateBuilder(args);

// === 서비스 등록 ===
builder.Services.AddControllers();

// AccountDatabase 싱글톤 인스턴스를 DI 컨테이너에 등록
// Factory 패턴으로 Instance 제공
builder.Services.AddSingleton<AccountDatabase>(serviceProvider => AccountDatabase.Instance);

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
app.UseCors("GameClient");
app.UseAuthorization();
app.MapControllers();

// 시작 로그
app.Logger.LogInformation("AuthServer starting on port {Port}", 
    app.Configuration["Urls"] ?? "https://localhost:7000");

app.Run();

public partial class Program { }