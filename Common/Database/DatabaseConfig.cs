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
        
        Console.WriteLine($"[DatabaseConfig] ===== Database Path Diagnostics =====");
        Console.WriteLine($"[DatabaseConfig] Current Directory: {currentDir}");
        Console.WriteLine($"[DatabaseConfig] Project Root: {projectRoot}");
        Console.WriteLine($"[DatabaseConfig] Database Directory: {dbDirectory}");
        
        // 디렉토리 존재 여부 확인
        if (!Directory.Exists(dbDirectory))
        {
            Console.WriteLine($"[DatabaseConfig] Creating database directory: {dbDirectory}");
            try
            {
                Directory.CreateDirectory(dbDirectory);
                Console.WriteLine($"[DatabaseConfig] Directory created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseConfig] ERROR creating directory: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[DatabaseConfig] Directory exists");
            
            // 디렉토리 권한 확인
            try
            {
                var testFile = Path.Combine(dbDirectory, ".permission_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                Console.WriteLine($"[DatabaseConfig] Directory is writable");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseConfig] WARNING: Directory not writable: {ex.Message}");
            }
        }
        
        var dbPath = Path.Combine(dbDirectory, "game.db");
        Console.WriteLine($"[DatabaseConfig] Full DB Path: {dbPath}");
        
        // 파일 존재 여부 확인
        if (File.Exists(dbPath))
        {
            var fileInfo = new FileInfo(dbPath);
            Console.WriteLine($"[DatabaseConfig] DB File exists:");
            Console.WriteLine($"[DatabaseConfig]   - Size: {fileInfo.Length} bytes");
            Console.WriteLine($"[DatabaseConfig]   - Created: {fileInfo.CreationTime}");
            Console.WriteLine($"[DatabaseConfig]   - Modified: {fileInfo.LastWriteTime}");
            
            // 파일 읽기/쓰기 권한 확인
            try
            {
                using var fs = File.Open(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Console.WriteLine($"[DatabaseConfig]   - File is accessible (ReadWrite)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseConfig]   - ERROR: Cannot access file: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[DatabaseConfig] DB File does not exist (will be created)");
        }
        
        Console.WriteLine($"[DatabaseConfig] =====================================");
        
        return dbPath;
    }
    
    private static string FindProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        
        while (dir != null)
        {
            // .sln 파일이 있는 곳을 프로젝트 루트로 판단
            if (dir.GetFiles("*.sln").Any())
            {
                Console.WriteLine($"[DatabaseConfig] Found project root via .sln: {dir.FullName}");
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        
        // 못 찾으면 현재 디렉토리 사용
        Console.WriteLine($"[DatabaseConfig] Using current directory as root: {startPath}");
        return startPath;
    }
    
    // Connection String을 여러 방식으로 시도
    public static string ConnectionString 
    {
        get
        {
            var dbPath = GetDatabasePath();
            
            // Mode=ReadWriteCreate 추가 - 파일이 없으면 생성
            var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
            
            Console.WriteLine($"[DatabaseConfig] Connection String: {connectionString}");
            return connectionString;
        }
    }
}