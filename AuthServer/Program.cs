using Common.Database;

var builder = WebApplication.CreateBuilder(args);

// === 서비스 등록 ===
builder.Services.AddControllers();

// AccountDatabase 싱글톤 인스턴스를 DI 컨테이너에 등록
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

// 추가: Production 환경에서 HTTPS 리디렉션 비활성화
if (builder.Environment.IsProduction())
{
    builder.WebHost.UseUrls("http://+:5006");
}

var app = builder.Build();

// 변경: Production에서는 HTTPS 리디렉션 사용 안함
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("GameClient");
app.UseAuthorization();
app.MapControllers();

// 시작 로그
app.Logger.LogInformation("AuthServer starting on {Environment} environment", 
    app.Environment.EnvironmentName);
app.Logger.LogInformation("Listening on: {Urls}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5006");

app.Run();

public partial class Program { }