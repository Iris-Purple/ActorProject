namespace Common.Database;

public static class DatabaseConfig
{
    // 프로젝트 루트에서 Database 폴더 경로 찾기
    public static string GetDatabasePath()
    {
        // 현재 실행 위치에서 상위 디렉토리로 올라가며 Database 폴더 찾기
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = FindProjectRoot(currentDir);
        
        var dbDirectory = Path.Combine(projectRoot, "Database");
        if (!Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
            Console.WriteLine($"[DatabaseConfig] Created database directory: {dbDirectory}");
        }
        
        // 변경: game.db 사용
        return Path.Combine(dbDirectory, "game.db");
    }
    
    private static string FindProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        
        while (dir != null)
        {
            // .sln 파일이 있는 곳을 프로젝트 루트로 판단
            if (dir.GetFiles("*.sln").Any())
            {
                Console.WriteLine($"[DatabaseConfig] Found project root: {dir.FullName}");
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        
        // 못 찾으면 현재 디렉토리 사용
        Console.WriteLine($"[DatabaseConfig] Using current directory as root: {startPath}");
        return startPath;
    }
    
    public static string ConnectionString => $"Data Source={GetDatabasePath()}";
}