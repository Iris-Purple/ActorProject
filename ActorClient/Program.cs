using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

class TerminalGameClient
{
    private TcpClient? client;
    private NetworkStream? stream;
    private bool running = true;
    private string playerName = "Unknown";
    private string currentZone = "Unknown";
    private string chatHistory = "";
    
    async Task Connect(string host, int port)
    {
        try
        {
            // 연결 시도
            AnsiConsole.Status()
                .Start("Connecting to server...", ctx =>
                {
                    client = new TcpClient();
                    client.Connect(host, port);
                    stream = client.GetStream();
                    ctx.Status("Connected!");
                });
            
            // 수신 스레드 시작
            _ = Task.Run(ReceiveLoop);
            
            // UI 실행
            await RunUI();
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }
    
    async Task RunUI()
    {
        while (running)
        {
            AnsiConsole.Clear();
            
            // 헤더
            var rule = new Rule($"[red]MMORPG Client[/] - [yellow]{playerName}[/] @ [cyan]{currentZone}[/]");
            AnsiConsole.Write(rule);
            
            // 채팅 히스토리
            var chatPanel = new Panel(string.IsNullOrEmpty(chatHistory) ? "[grey]No messages yet[/]" : chatHistory)
            {
                Header = new PanelHeader(" Chat History "),
                Border = BoxBorder.Rounded,
                Height = 15
            };
            AnsiConsole.Write(chatPanel);
            
            // 명령어 도움말
            var table = new Table();
            table.AddColumn("[yellow]Command[/]");
            table.AddColumn("[white]Description[/]");
            table.AddRow("/login <name>", "Login with your name");
            table.AddRow("/move <x> <y>", "Move to position");
            table.AddRow("/say <message>", "Send chat message");
            table.AddRow("/zone <name>", "Change zone (town/forest/dungeon-1)");
            table.AddRow("/quit", "Exit game");
            table.Border(TableBorder.Rounded);
            AnsiConsole.Write(table);
            
            // 입력 받기
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]>[/]")
                    .AllowEmpty());
            
            if (input == "/quit")
            {
                running = false;
                break;
            }
            
            if (!string.IsNullOrEmpty(input))
            {
                await SendMessage(input);
                // 서버 응답 받을 시간
                await Task.Delay(200);
            }
        }
    }
    
    async Task ReceiveLoop()
    {
        var buffer = new byte[1024];
        
        try
        {
            while (running && stream != null)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    ProcessServerMessage(message);
                }
            }
        }
        catch
        {
            running = false;
        }
    }
    
    void ProcessServerMessage(string message)
    {
        // 메시지 파싱
        if (message.StartsWith("Logged in as "))
        {
            playerName = message.Substring(13);
            AddToChatHistory($"[green]System: {message}[/]");
        }
        else if (message.StartsWith("Moving to zone:"))
        {
            currentZone = message.Substring(15).Trim();
            AddToChatHistory($"[yellow]System: Entered {currentZone}[/]");
        }
        else if (message.StartsWith("[") && message.Contains("]:"))
        {
            // 다른 플레이어 채팅
            AddToChatHistory($"[cyan]{message}[/]");
        }
        else if (message.StartsWith("You:"))
        {
            // 내 채팅
            AddToChatHistory($"[green]{message}[/]");
        }
        else
        {
            // 기타 메시지
            AddToChatHistory($"[grey]{message}[/]");
        }
    }
    
    void AddToChatHistory(string message)
    {
        // 최근 10개 메시지만 유지
        var lines = chatHistory.Split('\n');
        if (lines.Length > 10)
        {
            var recent = new string[10];
            Array.Copy(lines, lines.Length - 10, recent, 0, 10);
            chatHistory = string.Join('\n', recent);
        }
        
        if (!string.IsNullOrEmpty(chatHistory))
            chatHistory += "\n";
        
        chatHistory += message;
    }
    
    async Task SendMessage(string message)
    {
        if (stream != null)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
    
    static async Task Main()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("MMORPG")
                .Centered()
                .Color(Color.Red));
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Connecting to server at localhost:9999...[/]");
        
        var client = new TerminalGameClient();
        await client.Connect("localhost", 9999);
    }
}