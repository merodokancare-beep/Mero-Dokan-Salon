using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Data.SqlClient;

namespace MeroDokan
{
    public static class DatabaseHelper
    {
        public class DbConfig
        {
            public string Server { get; set; } = "(localdb)\\MSSQLLocalDB";
            public string Database { get; set; } = "MeroDokanSaloonDB";
            public bool IntegratedSecurity { get; set; } = true;
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public int ConnectionTimeout { get; set; } = 30;
            public int ConnectRetryCount { get; set; } = 3;
            public int ConnectRetryInterval { get; set; } = 10;
        }

        private static DbConfig _cachedConfig = null;
        private static string _cachedLocalDbServer = null;
        private static string _cachedLocalDbPipe = null;
        private static DateTime _lastResolvedTime = DateTime.MinValue;

        private static DbConfig GetCachedConfig()
        {
            if (_cachedConfig == null)
            {
                _cachedConfig = LoadConfig();
            }
            return _cachedConfig;
        }

        public static string ConnectionString
        {
            get
            {
                return BuildConnectionString(GetCachedConfig());
            }
            set
            {
            }
        }

        public static string MasterConnectionString
        {
            get
            {
                try
                {
                    var builder = new SqlConnectionStringBuilder(ConnectionString);
                    builder.InitialCatalog = "master";
                    return builder.ConnectionString;
                }
                catch
                {
                    return "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;";
                }
            }
            set
            {
            }
        }

        public static string GetConfigFilePath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFile = Path.Combine(appDir, "dbconfig.txt");
            try
            {
                // Test write permissions
                string testFile = Path.Combine(appDir, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return localFile;
            }
            catch
            {
                // Fallback to LocalApplicationData
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeroDokan");
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                return Path.Combine(appDataDir, "dbconfig.txt");
            }
        }

        public static DbConfig LoadConfig()
        {
            var config = new DbConfig();
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || !line.Contains("="))
                            continue;
                        
                        int idx = line.IndexOf('=');
                        string key = line.Substring(0, idx).Trim();
                        string val = line.Substring(idx + 1).Trim();

                        if (key.Equals("Server", StringComparison.OrdinalIgnoreCase)) config.Server = val;
                        else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)) config.Database = val;
                        else if (key.Equals("IntegratedSecurity", StringComparison.OrdinalIgnoreCase)) config.IntegratedSecurity = bool.Parse(val);
                        else if (key.Equals("Username", StringComparison.OrdinalIgnoreCase)) config.Username = val;
                        else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)) config.Password = val;
                        else if (key.Equals("ConnectionTimeout", StringComparison.OrdinalIgnoreCase)) config.ConnectionTimeout = int.Parse(val);
                        else if (key.Equals("ConnectRetryCount", StringComparison.OrdinalIgnoreCase)) config.ConnectRetryCount = int.Parse(val);
                        else if (key.Equals("ConnectRetryInterval", StringComparison.OrdinalIgnoreCase)) config.ConnectRetryInterval = int.Parse(val);
                    }
                }
                catch { }
            }
            else
            {
                config.Server = ResolveFirstRunServer();
                SaveConfig(config);
            }
            return config;
        }

        public static void SaveConfig(DbConfig config)
        {
            try
            {
                string path = GetConfigFilePath();
                var sb = new StringBuilder();
                sb.AppendLine("Server=" + config.Server);
                sb.AppendLine("Database=" + config.Database);
                sb.AppendLine("IntegratedSecurity=" + config.IntegratedSecurity.ToString());
                sb.AppendLine("Username=" + config.Username);
                sb.AppendLine("Password=" + config.Password);
                sb.AppendLine("ConnectionTimeout=" + config.ConnectionTimeout.ToString());
                sb.AppendLine("ConnectRetryCount=" + config.ConnectRetryCount.ToString());
                sb.AppendLine("ConnectRetryInterval=" + config.ConnectRetryInterval.ToString());
                File.WriteAllText(path, sb.ToString());
                
                _cachedConfig = config;

                // Discard stale connections
                SqlConnection.ClearAllPools();
            }
            catch { }
        }

        private static void LoadConnectionString()
        {
            GetCachedConfig();
        }

        private static bool? _isConnectRetrySupported = null;
        public static bool IsConnectRetrySupported
        {
            get
            {
                if (!_isConnectRetrySupported.HasValue)
                {
                    _isConnectRetrySupported = TestKeywordSupport("Connect Retry Count");
                }
                return _isConnectRetrySupported.Value;
            }
        }

        private static bool TestKeywordSupport(string keyword)
        {
            try
            {
                using (var conn = new SqlConnection("Server=dummy;" + keyword + "=1;"))
                {
                    string s = conn.ConnectionString;
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch
            {
                return true;
            }
        }

        public static string BuildConnectionString(DbConfig config)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder();
                builder.DataSource = ResolveLocalDbServerName(config.Server);
                builder.InitialCatalog = config.Database;
                builder.IntegratedSecurity = config.IntegratedSecurity;
                if (!config.IntegratedSecurity)
                {
                    builder.UserID = config.Username;
                    builder.Password = config.Password;
                }
                builder.ConnectTimeout = config.ConnectionTimeout;
                
                string connStr = builder.ConnectionString;
                if (!connStr.EndsWith(";"))
                    connStr += ";";
                
                connStr += "Encrypt=False;TrustServerCertificate=True;";
                
                if (IsConnectRetrySupported)
                {
                    connStr += "Connect Retry Count=" + config.ConnectRetryCount + ";";
                    connStr += "Connect Retry Interval=" + config.ConnectRetryInterval + ";";
                }
                
                return connStr;
            }
            catch
            {
                return "Server=(localdb)\\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;";
            }
        }

        private static string FindSqlLocalDBPath()
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sqllocaldb",
                    Arguments = "-v",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var proc = System.Diagnostics.Process.Start(info))
                {
                    proc.WaitForExit(1000);
                    return "sqllocaldb";
                }
            }
            catch { }

            var searchFolders = new System.Collections.Generic.List<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            string[] versions = { "160", "150", "140", "130", "120", "110" };
            foreach (var ver in versions)
            {
                if (!string.IsNullOrEmpty(pf))
                {
                    searchFolders.Add(Path.Combine(pf, @"Microsoft SQL Server\" + ver + @"\Tools\Binn"));
                }
                if (!string.IsNullOrEmpty(pf86))
                {
                    searchFolders.Add(Path.Combine(pf86, @"Microsoft SQL Server\" + ver + @"\Tools\Binn"));
                }
            }

            foreach (var folder in searchFolders)
            {
                string fullPath = Path.Combine(folder, "SqlLocalDB.exe");
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private static System.Collections.Generic.List<string> GetLocalDBInstances(string localDbPath)
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = localDbPath,
                    Arguments = "info",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var proc = System.Diagnostics.Process.Start(info))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            string trimmed = line.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("Microsoft"))
                            {
                                list.Add(trimmed);
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static void GetLocalDBInfo(string localDbPath, string instanceName, out string state, out string pipeName)
        {
            state = "Stopped";
            pipeName = null;
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = localDbPath,
                    Arguments = "info \"" + instanceName + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var proc = System.Diagnostics.Process.Start(info))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            string trimmed = line.Trim();
                            if (trimmed.StartsWith("State:", StringComparison.OrdinalIgnoreCase))
                            {
                                int idx = trimmed.IndexOf(':');
                                if (idx != -1)
                                {
                                    state = trimmed.Substring(idx + 1).Trim();
                                }
                            }
                            int pipeIdx = trimmed.IndexOf("np:", StringComparison.OrdinalIgnoreCase);
                            if (pipeIdx != -1)
                            {
                                pipeName = trimmed.Substring(pipeIdx).Trim();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static string GetLocalDBDiagnostics()
        {
            var sb = new StringBuilder();
            string localDbPath = FindSqlLocalDBPath();
            if (string.IsNullOrEmpty(localDbPath))
            {
                sb.AppendLine("sqllocaldb utility not found in PATH or standard Program Files folders.");
                return sb.ToString();
            }

            sb.AppendLine("sqllocaldb executable path: " + localDbPath);

            // Run sqllocaldb -v
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = localDbPath,
                    Arguments = "-v",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var proc = System.Diagnostics.Process.Start(info))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(2000);
                    sb.AppendLine("Version: " + stdout.Trim() + " " + stderr.Trim());
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Failed to run version check: " + ex.Message);
            }

            // Run sqllocaldb info
            System.Collections.Generic.List<string> instances = null;
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = localDbPath,
                    Arguments = "info",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                using (var proc = System.Diagnostics.Process.Start(info))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(2000);
                    sb.AppendLine("Instances:\n" + stdout.Trim() + " " + stderr.Trim());

                    instances = new System.Collections.Generic.List<string>();
                    string[] lines = stdout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("Microsoft"))
                        {
                            instances.Add(trimmed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Failed to run info check: " + ex.Message);
            }

            // Run sqllocaldb info <instance>
            if (instances != null && instances.Count > 0)
            {
                foreach (var inst in instances)
                {
                    try
                    {
                        var info = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = localDbPath,
                            Arguments = "info \"" + inst + "\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        };
                        using (var proc = System.Diagnostics.Process.Start(info))
                        {
                            string stdout = proc.StandardOutput.ReadToEnd();
                            string stderr = proc.StandardError.ReadToEnd();
                            proc.WaitForExit(2000);
                            sb.AppendLine("\nInstance Details (" + inst + "):\n" + stdout.Trim() + " " + stderr.Trim());
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine("Failed to run info for " + inst + ": " + ex.Message);
                    }
                }
            }

            return sb.ToString();
        }

        private static void TryStartLocalDB()
        {
            string localDbPath = FindSqlLocalDBPath();
            if (string.IsNullOrEmpty(localDbPath))
            {
                return;
            }

            var instances = GetLocalDBInstances(localDbPath);
            
            // Ensure default instances are created if not present
            string[] defaultInstances = { "MSSQLLocalDB", "v11.0" };
            foreach (var defaultInst in defaultInstances)
            {
                bool exists = false;
                foreach (var inst in instances)
                {
                    if (string.Equals(inst, defaultInst, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    try
                    {
                        var createInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = localDbPath,
                            Arguments = "create " + defaultInst,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        };
                        using (var proc = System.Diagnostics.Process.Start(createInfo))
                        {
                            proc.WaitForExit(5000);
                        }
                    }
                    catch { }
                }
            }

            // Refresh instances list
            instances = GetLocalDBInstances(localDbPath);

            foreach (var instance in instances)
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = localDbPath,
                        Arguments = "start \"" + instance + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    using (var proc = System.Diagnostics.Process.Start(startInfo))
                    {
                        proc.WaitForExit(3000);
                    }
                }
                catch
                {
                }
            }
        }

        public static void ResolveConnectionStrings()
        {
            _cachedLocalDbPipe = null;
            _lastResolvedTime = DateTime.MinValue;
            GetCachedConfig();
        }

        public static string ResolveFirstRunServer()
        {
            TryStartLocalDB();

            var serverList = new System.Collections.Generic.List<string>();

            // 1. Add dynamically discovered LocalDB instances first
            string localDbPath = FindSqlLocalDBPath();
            if (!string.IsNullOrEmpty(localDbPath))
            {
                var instances = GetLocalDBInstances(localDbPath);
                foreach (var inst in instances)
                {
                    string srvName = "(localdb)\\" + inst;
                    if (!serverList.Contains(srvName))
                    {
                        serverList.Add(srvName);
                    }
                }
            }

            // 2. Add standard static server names
            string[] standardServers = {
                "(localdb)\\MSSQLLocalDB",
                "(localdb)\\v11.0",
                ".\\SQLEXPRESS",
                "localhost\\SQLEXPRESS",
                "(local)\\SQLEXPRESS",
                "localhost",
                "."
            };

            foreach (var srv in standardServers)
            {
                if (!serverList.Contains(srv))
                {
                    serverList.Add(srv);
                }
            }

            // 3. Probe each server connection
            foreach (string server in serverList)
            {
                string testServer = server;
                if (server.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
                {
                    string instanceName = server.Substring(10).Trim();
                    string state;
                    string pipeName;
                    GetLocalDBInfo(localDbPath, instanceName, out state, out pipeName);
                    if (!string.IsNullOrEmpty(pipeName))
                    {
                        testServer = pipeName;
                    }
                }

                string masterTest = "Server=" + testServer + ";Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connection Timeout=2;";
                try
                {
                    using (SqlConnection conn = new SqlConnection(masterTest))
                    {
                        conn.Open();
                        return server;
                    }
                }
                catch
                {
                    // Try next
                }
            }

            // Fallback: use the first server we probed, or (localdb)\MSSQLLocalDB if none
            return serverList.Count > 0 ? serverList[0] : "(localdb)\\MSSQLLocalDB";
        }

        public static string ResolveLocalDbServerName(string serverName)
        {
            if (string.IsNullOrEmpty(serverName))
                return serverName;

            if (serverName.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
            {
                if (serverName.Equals(_cachedLocalDbServer, StringComparison.OrdinalIgnoreCase) && 
                    (DateTime.UtcNow - _lastResolvedTime).TotalSeconds < 10 &&
                    !string.IsNullOrEmpty(_cachedLocalDbPipe))
                {
                    return _cachedLocalDbPipe;
                }

                string instanceName = serverName.Substring(10).Trim();
                string localDbPath = FindSqlLocalDBPath();
                if (!string.IsNullOrEmpty(localDbPath))
                {
                    string state = "Stopped";
                    string pipeName = null;

                    // 1. Get initial state
                    GetLocalDBInfo(localDbPath, instanceName, out state, out pipeName);

                    // 2. If it is stopped or starting, trigger start command and clear connection pools
                    bool wasStopped = !state.Equals("Running", StringComparison.OrdinalIgnoreCase);
                    if (wasStopped)
                    {
                        try
                        {
                            SqlConnection.ClearAllPools();
                        }
                        catch { }

                        try
                        {
                            var startInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = localDbPath,
                                Arguments = "start \"" + instanceName + "\"",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                            };
                            using (var proc = System.Diagnostics.Process.Start(startInfo))
                            {
                                proc.WaitForExit(3000);
                            }
                        }
                        catch { }
                    }

                    // 3. Poll until state is "Running" and pipeName is available (up to 10 seconds timeout)
                    int attempts = 0;
                    while (attempts < 20) // 20 * 500ms = 10 seconds
                    {
                        GetLocalDBInfo(localDbPath, instanceName, out state, out pipeName);
                        if (state.Equals("Running", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(pipeName))
                        {
                            break;
                        }
                        System.Threading.Thread.Sleep(500);
                        attempts++;
                    }

                    if (!string.IsNullOrEmpty(pipeName))
                    {
                        _cachedLocalDbServer = serverName;
                        _cachedLocalDbPipe = pipeName;
                        _lastResolvedTime = DateTime.UtcNow;
                        return pipeName;
                    }
                }
            }
            return serverName;
        }

        public static void InitializeDatabase()
        {
            try
            {
                // 1. Create Database if it doesn't exist
                using (SqlConnection masterConn = new SqlConnection(MasterConnectionString))
                {
                    masterConn.Open();
                    bool dbExists = false;
                    using (SqlCommand cmd = new SqlCommand("SELECT database_id FROM sys.databases WHERE name = 'MeroDokanSaloonDB'", masterConn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            dbExists = true;
                        }
                    }

                    if (!dbExists)
                    {
                        using (SqlCommand cmd = new SqlCommand("CREATE DATABASE MeroDokanSaloonDB", masterConn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // 2. Create Tables inside MeroDokanSaloonDB
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Users Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
                        BEGIN
                            CREATE TABLE Users (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Username NVARCHAR(50) NOT NULL UNIQUE,
                                PasswordHash NVARCHAR(255) NOT NULL,
                                FullName NVARCHAR(100) NOT NULL,
                                Role NVARCHAR(20) NOT NULL,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Customers Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Customers')
                        BEGIN
                            CREATE TABLE Customers (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Name NVARCHAR(100) NOT NULL,
                                Phone NVARCHAR(20) NULL,
                                Email NVARCHAR(100) NULL,
                                Address NVARCHAR(200) NULL,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Suppliers Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
                        BEGIN
                            CREATE TABLE Suppliers (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Name NVARCHAR(100) NOT NULL,
                                ContactPerson NVARCHAR(100) NULL,
                                Phone NVARCHAR(20) NULL,
                                Email NVARCHAR(100) NULL,
                                Address NVARCHAR(200) NULL,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Categories Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
                        BEGIN
                            CREATE TABLE Categories (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Name NVARCHAR(100) UNIQUE NOT NULL,
                                Type NVARCHAR(20) NOT NULL DEFAULT 'Service',
                                HsnSacCode NVARCHAR(50) NULL DEFAULT '999721',
                                GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00
                            )
                        END", conn);

                    // Products Table (Retail beauty / grooming inventory)
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
                        BEGIN
                            CREATE TABLE Products (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Code NVARCHAR(50) NOT NULL UNIQUE,
                                Name NVARCHAR(150) NOT NULL,
                                Description NVARCHAR(500) NULL,
                                Category NVARCHAR(100) NULL,
                                PurchasePrice DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                SalesPrice DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                Stock INT NOT NULL DEFAULT 0,
                                MinStockLevel INT NOT NULL DEFAULT 5,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Services Table (Salon treatments, haircuts, spas, styling)
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Services')
                        BEGIN
                            CREATE TABLE Services (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Code NVARCHAR(50) NOT NULL UNIQUE,
                                Name NVARCHAR(150) NOT NULL,
                                Category NVARCHAR(100) NULL,
                                Price DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                DurationMinutes INT NOT NULL DEFAULT 30,
                                Description NVARCHAR(500) NULL,
                                IsActive BIT NOT NULL DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Stylist Roles Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StylistRoles')
                        BEGIN
                            CREATE TABLE StylistRoles (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                RoleName NVARCHAR(100) NOT NULL UNIQUE,
                                Description NVARCHAR(500) NULL,
                                DefaultCommissionRate DECIMAL(5,2) NOT NULL DEFAULT 10.00,
                                IsActive BIT NOT NULL DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            );

                            INSERT INTO StylistRoles (RoleName, Description, DefaultCommissionRate, IsActive) VALUES
                            ('Senior Hair Stylist', 'Expert haircuts, styling, and hair transformations', 15.00, 1),
                            ('Hair Stylist', 'Standard haircuts, hair spa, and styling treatments', 10.00, 1),
                            ('Master Barber & Groomer', 'Beard grooming, royal shaves, and mens hair grooming', 12.00, 1),
                            ('Beautician & Skin Specialist', 'Facial therapies, skin treatments, waxing, and cleanups', 12.00, 1),
                            ('Colorist & Chemical Specialist', 'Hair coloring, highlights, smoothening, and keratin treatments', 15.00, 1),
                            ('Spa & Massage Therapist', 'Full body spas, head massage, reflexology, and relaxation therapies', 15.00, 1),
                            ('Nail Artist & Pedicurist', 'Manicure, pedicure, nail art, and nail extensions', 10.00, 1),
                            ('Junior Stylist / Apprentice', 'Entry-level styling and service support', 5.00, 1),
                            ('Salon Assistant', 'Shampooing, blow-drying, and general service assistance', 5.00, 1);
                        END", conn);

                    // Staff / Stylists Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Staff')
                        BEGIN
                            CREATE TABLE Staff (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Name NVARCHAR(100) NOT NULL,
                                Phone NVARCHAR(50) NULL,
                                Email NVARCHAR(100) NULL,
                                Role NVARCHAR(50) NOT NULL DEFAULT 'Stylist',
                                CommissionRate DECIMAL(5,2) NOT NULL DEFAULT 10.00,
                                IsActive BIT NOT NULL DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Appointments Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appointments')
                        BEGIN
                            CREATE TABLE Appointments (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                AppointmentNumber NVARCHAR(50) NOT NULL UNIQUE,
                                CustomerId INT NULL FOREIGN KEY REFERENCES Customers(Id) ON DELETE SET NULL,
                                StaffId INT NULL FOREIGN KEY REFERENCES Staff(Id) ON DELETE SET NULL,
                                ServiceId INT NULL FOREIGN KEY REFERENCES Services(Id) ON DELETE SET NULL,
                                ServiceIds NVARCHAR(500) NULL,
                                ServiceNames NVARCHAR(1000) NULL,
                                AppointmentDate DATE NOT NULL,
                                AppointmentTime NVARCHAR(100) NOT NULL,
                                Status NVARCHAR(30) NOT NULL DEFAULT 'Booked',
                                Notes NVARCHAR(500) NULL,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // Purchases Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Purchases')
                        BEGIN
                            CREATE TABLE Purchases (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                PurchaseNumber NVARCHAR(50) NOT NULL UNIQUE,
                                SupplierId INT NULL FOREIGN KEY REFERENCES Suppliers(Id) ON DELETE SET NULL,
                                PurchaseDate DATETIME NOT NULL DEFAULT GETDATE(),
                                TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                CreatedBy INT NULL FOREIGN KEY REFERENCES Users(Id)
                            )
                        END", conn);

                    // PurchaseDetails Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseDetails')
                        BEGIN
                            CREATE TABLE PurchaseDetails (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                PurchaseId INT FOREIGN KEY REFERENCES Purchases(Id) ON DELETE CASCADE,
                                ProductId INT FOREIGN KEY REFERENCES Products(Id) ON DELETE CASCADE,
                                Quantity INT NOT NULL,
                                PurchasePrice DECIMAL(18,2) NOT NULL
                            )
                        END", conn);

                    // Sales Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sales')
                        BEGIN
                            CREATE TABLE Sales (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                InvoiceNumber NVARCHAR(50) NOT NULL UNIQUE,
                                CustomerId INT NULL FOREIGN KEY REFERENCES Customers(Id) ON DELETE SET NULL,
                                SaleDate DATETIME NOT NULL DEFAULT GETDATE(),
                                SubTotal DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                Discount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                Tax DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                GrandTotal DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                DueAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Cash',
                                CreatedBy INT NULL FOREIGN KEY REFERENCES Users(Id)
                            )
                        END
                        ELSE
                        BEGIN
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'AmountPaid')
                            BEGIN
                                ALTER TABLE Sales ADD AmountPaid DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                            END
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'DueAmount')
                            BEGIN
                                ALTER TABLE Sales ADD DueAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                            END
                        END", conn);

                    // SaleDetails Table (Supports both Products and Services with assigned Stylist)
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SaleDetails')
                        BEGIN
                            CREATE TABLE SaleDetails (
                                  Id INT PRIMARY KEY IDENTITY(1,1),
                                  SaleId INT FOREIGN KEY REFERENCES Sales(Id) ON DELETE CASCADE,
                                  ItemType NVARCHAR(20) NOT NULL DEFAULT 'Product',
                                  ProductId INT NULL FOREIGN KEY REFERENCES Products(Id) ON DELETE CASCADE,
                                  ServiceId INT NULL FOREIGN KEY REFERENCES Services(Id) ON DELETE SET NULL,
                                  StaffId INT NULL FOREIGN KEY REFERENCES Staff(Id) ON DELETE SET NULL,
                                  Quantity INT NOT NULL,
                                  UnitPrice DECIMAL(18,2) NOT NULL,
                                  Total DECIMAL(18,2) NOT NULL,
                                  PurchaseCostAtSale DECIMAL(18,2) NOT NULL DEFAULT 0.00
                            )
                        END
                        ELSE
                        BEGIN
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'ItemType')
                            BEGIN
                                ALTER TABLE SaleDetails ADD ItemType NVARCHAR(20) NOT NULL DEFAULT 'Product';
                            END
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'ServiceId')
                            BEGIN
                                ALTER TABLE SaleDetails ADD ServiceId INT NULL FOREIGN KEY REFERENCES Services(Id) ON DELETE SET NULL;
                            END
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'StaffId')
                            BEGIN
                                ALTER TABLE SaleDetails ADD StaffId INT NULL FOREIGN KEY REFERENCES Staff(Id) ON DELETE SET NULL;
                            END
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'PurchaseCostAtSale')
                            BEGIN
                                ALTER TABLE SaleDetails ADD PurchaseCostAtSale DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                            END
                        END", conn);

                    // ProductPriceHistory Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductPriceHistory')
                        BEGIN
                            CREATE TABLE ProductPriceHistory (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id) ON DELETE CASCADE,
                                OldPurchasePrice DECIMAL(18,2) NOT NULL,
                                NewPurchasePrice DECIMAL(18,2) NOT NULL,
                                OldSalesPrice DECIMAL(18,2) NOT NULL,
                                NewSalesPrice DECIMAL(18,2) NOT NULL,
                                ChangeDate DATETIME NOT NULL DEFAULT GETDATE(),
                                ChangedBy INT NULL FOREIGN KEY REFERENCES Users(Id),
                                Source NVARCHAR(100) NOT NULL
                            )
                        END", conn);

                    // SalesReturns Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SalesReturns')
                        BEGIN
                            CREATE TABLE SalesReturns (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                ReturnNumber NVARCHAR(50) UNIQUE NOT NULL,
                                SaleId INT NOT NULL FOREIGN KEY REFERENCES Sales(Id) ON DELETE CASCADE,
                                ReturnDate DATETIME NOT NULL DEFAULT GETDATE(),
                                TotalRefund DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                CashRefund DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                CreatedBy INT NULL FOREIGN KEY REFERENCES Users(Id)
                            )
                        END", conn);

                    // SalesReturnDetails Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SalesReturnDetails')
                        BEGIN
                            CREATE TABLE SalesReturnDetails (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                ReturnId INT NOT NULL FOREIGN KEY REFERENCES SalesReturns(Id) ON DELETE CASCADE,
                                ProductId INT NOT NULL FOREIGN KEY REFERENCES Products(Id),
                                Quantity INT NOT NULL,
                                RefundPrice DECIMAL(18,2) NOT NULL,
                                Total DECIMAL(18,2) NOT NULL,
                                ItemCondition NVARCHAR(50) NOT NULL
                            )
                        END", conn);

                    // CustomerPayments Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CustomerPayments')
                        BEGIN
                            CREATE TABLE CustomerPayments (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                CustomerId INT NOT NULL FOREIGN KEY REFERENCES Customers(Id) ON DELETE CASCADE,
                                PaymentDate DATETIME NOT NULL DEFAULT GETDATE(),
                                Amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Cash',
                                Remarks NVARCHAR(200) NULL,
                                CreatedBy INT NULL FOREIGN KEY REFERENCES Users(Id),
                                SaleId INT NULL FOREIGN KEY REFERENCES Sales(Id) ON DELETE SET NULL
                            )
                        END", conn);

                    // DailySettlements Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailySettlements')
                        BEGIN
                            CREATE TABLE DailySettlements (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                SettlementDate DATETIME NOT NULL DEFAULT GETDATE(),
                                OpeningCash DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                CashSales DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                DueCollections DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                CardQRSales DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                CardSales DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                QRSales DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                DuesCreated DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                ExpectedCash DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                ActualCash DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                Variance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                                SettlementBy INT NULL FOREIGN KEY REFERENCES Users(Id),
                                Remarks NVARCHAR(500) NULL,
                                Refunds DECIMAL(18,2) NOT NULL DEFAULT 0.00
                            )
                        END", conn);

                    // AppProfile Configuration Table
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppProfile')
                        BEGIN
                            CREATE TABLE AppProfile (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                OwnerName NVARCHAR(100) NOT NULL DEFAULT 'Saloon Manager',
                                ShopName NVARCHAR(150) NOT NULL DEFAULT 'Mero Dokan Saloon & Spa',
                                Phone NVARCHAR(50) NOT NULL DEFAULT '+977-1-4200000',
                                Email NVARCHAR(100) NOT NULL DEFAULT 'contact@merosaloon.com',
                                Address NVARCHAR(200) NOT NULL DEFAULT 'Kathmandu, Nepal',
                                LogoPath NVARCHAR(500) NULL,
                                ProfilePicPath NVARCHAR(500) NULL,
                                ThemePreset NVARCHAR(50) NOT NULL DEFAULT 'Rose Gold',
                                FontSizePreset NVARCHAR(50) NOT NULL DEFAULT 'Medium',
                                BackupFolderPath NVARCHAR(500) NOT NULL DEFAULT 'D:\MeroDokanSaloon\DailyDatabaseBackup',
                                GoogleDriveAddress NVARCHAR(500) NOT NULL DEFAULT 'https://script.google.com/macros/s/AKfycbwm3WKMbeToLZt10WTPGrHwL4XsA8JgVO_H4MAaraDpssgTfUNs1x_ECblU4cKkRMAx/exec',
                                GSTIN NVARCHAR(50) NULL,
                                UPIId NVARCHAR(100) NULL,
                                UPIName NVARCHAR(100) NULL,
                                AutoShowQROnUPI BIT NOT NULL DEFAULT 1,
                                PrintQROnReceipt BIT NOT NULL DEFAULT 1
                            )
                        END", conn);

                    // HsnSacMaster Table (Harmonized System of Nomenclature & Services Accounting Code)
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HsnSacMaster')
                        BEGIN
                            CREATE TABLE HsnSacMaster (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Code NVARCHAR(50) NOT NULL UNIQUE,
                                Type NVARCHAR(20) NOT NULL DEFAULT 'HSN',
                                Description NVARCHAR(500) NOT NULL,
                                GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00,
                                IsActive BIT NOT NULL DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            )
                        END", conn);

                    // 3. Seed Default Admin User if none exists
                    int userCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Users", conn))
                    {
                        userCount = (int)cmd.ExecuteScalar();
                    }

                    if (userCount == 0)
                    {
                        string adminPassHash = HashPassword("admin");
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Users (Username, PasswordHash, FullName, Role) 
                            VALUES (@username, @password, @fullname, @role)", conn))
                        {
                            cmd.Parameters.AddWithValue("@username", "admin");
                            cmd.Parameters.AddWithValue("@password", adminPassHash);
                            cmd.Parameters.AddWithValue("@fullname", "System Administrator");
                            cmd.Parameters.AddWithValue("@role", "Admin");
                            cmd.ExecuteNonQuery();
                        }

                        // Seed default customers and suppliers for salon presentation
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Customers (Name, Phone, Email, Address) VALUES 
                            ('Walk-in Client', '0000000000', 'walkin@merosaloon.com', 'Local'),
                            ('Aarav Sharma', '9841234567', 'aarav@gmail.com', 'Kathmandu'),
                            ('Sneha Karki', '9851234567', 'sneha@yahoo.com', 'Lalitpur');
                            
                            INSERT INTO Suppliers (Name, ContactPerson, Phone, Email, Address) VALUES 
                            ('L''Oreal Nepal Distributors', 'Ramesh Sen', '9801122334', 'loreal.dist@gmail.com', 'New Road, Kathmandu'),
                            ('Beauty & Spa Essentials Supply', 'Binod Chaudhary', '9812233445', 'beautyspa@supply.com', 'Birgunj');", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 4. Seed Default Categories if none exist
                    int categoryCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Categories", conn))
                    {
                        categoryCount = (int)cmd.ExecuteScalar();
                    }

                    if (categoryCount == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Categories (Name, Type, HsnSacCode, GSTRate) VALUES 
                            ('Hair Services', 'Service', '999721', 18.00),
                            ('Beard & Grooming', 'Service', '999721', 18.00),
                            ('Facial & Skin Care', 'Service', '999722', 18.00),
                            ('Hair Spa & Treatments', 'Service', '999721', 18.00),
                            ('Body Massage & Spa', 'Service', '999729', 18.00),
                            ('Manicure & Pedicure', 'Service', '999722', 18.00),
                            ('Hair Care Products', 'Product', '3305', 18.00),
                            ('Skin Care Products', 'Product', '3304', 18.00),
                            ('Grooming Accessories', 'Product', '8214', 18.00)", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 5. Seed Default Staff if none exist
                    int staffCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Staff", conn))
                    {
                        staffCount = (int)cmd.ExecuteScalar();
                    }

                    if (staffCount == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Staff (Name, Phone, Email, Role, CommissionRate, IsActive) VALUES 
                            ('Rahul Sharma', '9841001122', 'rahul@merosaloon.com', 'Senior Hair Stylist', 15.00, 1),
                            ('Priya Thapa', '9851002233', 'priya@merosaloon.com', 'Beautician & Skin Care', 12.00, 1),
                            ('Alex Shrestha', '9801003344', 'alex@merosaloon.com', 'Master Barber & Groomer', 10.00, 1),
                            ('Maya Gurung', '9811004455', 'maya@merosaloon.com', 'Spa & Massage Therapist', 15.00, 1)", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 6. Seed Default Saloon Services if none exist
                    int serviceCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Services", conn))
                    {
                        serviceCount = (int)cmd.ExecuteScalar();
                    }

                    if (serviceCount == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Services (Code, Name, Category, Price, DurationMinutes, Description, IsActive) VALUES 
                            ('SRV-101', 'Men''s Classic Haircut & Style', 'Hair Services', 350.00, 30, 'Classic men hair styling and wash', 1),
                            ('SRV-102', 'Women''s Precision Haircut & Blowdry', 'Hair Services', 800.00, 45, 'Complete wash, cut and blowout', 1),
                            ('SRV-103', 'Royal Beard Shaping & Hot Towel Trim', 'Beard & Grooming', 250.00, 25, 'Luxury beard sculpting with hot towel', 1),
                            ('SRV-104', 'Deep Cleansing Gold Facial', 'Facial & Skin Care', 1500.00, 60, 'Revitalizing anti-aging skin therapy', 1),
                            ('SRV-105', 'Intensive Keratin Hair Spa Treatment', 'Hair Spa & Treatments', 2200.00, 75, 'Deep nourishment keratin hair repair', 1),
                            ('SRV-106', 'Global Hair Color / Highlights', 'Hair Services', 2800.00, 90, 'Premium ammonia-free hair color', 1),
                            ('SRV-107', 'Ayurvedic Head Massage & Oil Spa', 'Body Massage & Spa', 600.00, 40, 'Relaxing herbal stress relief head massage', 1),
                            ('SRV-108', 'Deluxe Manicure & Pedicure Combo', 'Manicure & Pedicure', 1200.00, 50, 'Nail shaping, scrub, polish and massage', 1)", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 7. Seed Default Saloon Retail Products if none exist
                    int productCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Products", conn))
                    {
                        productCount = (int)cmd.ExecuteScalar();
                    }

                    if (productCount == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Products (Code, Name, Description, Category, PurchasePrice, SalesPrice, Stock, MinStockLevel) VALUES 
                            ('PRD-101', 'Moroccan Argan Hair Serum 100ml', 'Glossy hair protection serum', 'Hair Care Products', 650.00, 1100.00, 25, 5),
                            ('PRD-102', 'Keratin Smooth Shampoo 300ml', 'Sulphate-free salon shampoo', 'Hair Care Products', 480.00, 850.00, 30, 5),
                            ('PRD-103', 'Matte Hold Hair Styling Clay Wax 100g', 'Strong hold natural finish clay', 'Hair Care Products', 350.00, 650.00, 20, 5),
                            ('PRD-104', 'Organic Cedarwood Beard Oil 50ml', 'Softening and shine beard oil', 'Grooming Accessories', 400.00, 750.00, 15, 3),
                            ('PRD-105', 'Tea Tree Purifying Face Wash 150ml', 'Oil control clarifying face wash', 'Skin Care Products', 320.00, 550.00, 25, 5)", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 8. Seed Default AppProfile if none exists
                    int profileCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM AppProfile", conn))
                    {
                        profileCount = (int)cmd.ExecuteScalar();
                    }

                    if (profileCount == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO AppProfile (OwnerName, ShopName, Phone, Email, Address, ThemePreset, BackupFolderPath, GoogleDriveAddress) 
                             VALUES ('Saloon Manager', 'Mero Dokan Saloon & Spa', '+977-1-4200000', 'contact@merosaloon.com', 'Kathmandu, Nepal', 'Rose Gold', 'D:\MeroDokanSaloon\DailyDatabaseBackup', 'https://script.google.com/macros/s/AKfycbwm3WKMbeToLZt10WTPGrHwL4XsA8JgVO_H4MAaraDpssgTfUNs1x_ECblU4cKkRMAx/exec')", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 9. Seed Default HSN & SAC Masters if none exist
                    int hsnSacCount = 0;
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM HsnSacMaster", conn))
                    {
                        hsnSacCount = (int)cmd.ExecuteScalar();
                    }

                    if (hsnSacCount == 0)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO HsnSacMaster (Code, Type, Description, GSTRate, IsActive) VALUES 
                            ('999721', 'SAC', 'Hairdressing and barbers services (haircut, hair wash, blowdry, styling, beard trim, hair color)', 18.00, 1),
                            ('999722', 'SAC', 'Cosmetic and beauty treatment services including manicure, pedicure, facial, waxing, makeup', 18.00, 1),
                            ('999729', 'SAC', 'Other beauty and wellness treatment services including spa therapies, sauna, relaxing massage', 18.00, 1),
                            ('999723', 'SAC', 'Physical well-being services and body treatments', 18.00, 1),
                            ('998399', 'SAC', 'Other professional, technical and business salon consulting services', 18.00, 1),
                            ('3305', 'HSN', 'Preparations for use on hair: shampoos, hair creams, dyes, bleaches, styling sprays, hair oils', 18.00, 1),
                            ('3304', 'HSN', 'Beauty or make-up preparations & skin care: face creams, serums, lotions, cleansers, manicures/pedicures', 18.00, 1),
                            ('3307', 'HSN', 'Pre-shave, shaving or after-shave preparations, personal deodorants, bath preparations', 18.00, 1),
                            ('3303', 'HSN', 'Perfumes and toilet waters / body sprays', 18.00, 1),
                            ('3401', 'HSN', 'Soap, organic surface-active products and preparations for washing the skin', 18.00, 1),
                            ('8214', 'HSN', 'Hair clippers, razors, scissors, manicure/pedicure sets and instruments', 18.00, 1),
                            ('8516', 'HSN', 'Electro-thermic hair dressing appliances: hair dryers, hair straighteners, hair curling tongs', 18.00, 1),
                            ('9615', 'HSN', 'Combs, hair-slides, hairpins, hair curlers and salon styling accessories', 18.00, 1),
                            ('3004', 'HSN', 'Medicament preparations, antiseptic lotions and medicated skin care ointments', 12.00, 1),
                            ('4818', 'HSN', 'Sanitary salon paper towels, facial tissues, neck strips, disposable wipes', 18.00, 1)", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Run column migrations for existing databases
                    ExecuteNonQuery(@"
                        -- AppProfile migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'GSTIN')
                            ALTER TABLE AppProfile ADD GSTIN NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'StateName')
                            ALTER TABLE AppProfile ADD StateName NVARCHAR(100) NOT NULL DEFAULT 'Delhi';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'StateCode')
                            ALTER TABLE AppProfile ADD StateCode NVARCHAR(10) NOT NULL DEFAULT '07';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'IsTaxInclusive')
                            ALTER TABLE AppProfile ADD IsTaxInclusive BIT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'DefaultBillType')
                            ALTER TABLE AppProfile ADD DefaultBillType NVARCHAR(50) NOT NULL DEFAULT 'GST';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'DefaultGSTRate')
                            ALTER TABLE AppProfile ADD DefaultGSTRate DECIMAL(18,2) NOT NULL DEFAULT 18.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'UPIId')
                            ALTER TABLE AppProfile ADD UPIId NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'UPIName')
                            ALTER TABLE AppProfile ADD UPIName NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'AutoShowQROnUPI')
                            ALTER TABLE AppProfile ADD AutoShowQROnUPI BIT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'PrintQROnReceipt')
                            ALTER TABLE AppProfile ADD PrintQROnReceipt BIT NOT NULL DEFAULT 1;

                        -- Services migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Services') AND name = 'SACCode')
                            ALTER TABLE Services ADD SACCode NVARCHAR(50) NOT NULL DEFAULT '999721';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Services') AND name = 'GSTRate')
                            ALTER TABLE Services ADD GSTRate DECIMAL(18,2) NOT NULL DEFAULT 18.00;

                        -- Products migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'HSNCode')
                            ALTER TABLE Products ADD HSNCode NVARCHAR(50) NOT NULL DEFAULT '3305';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'GSTRate')
                            ALTER TABLE Products ADD GSTRate DECIMAL(18,2) NOT NULL DEFAULT 18.00;

                        -- Customers migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'GSTIN')
                            ALTER TABLE Customers ADD GSTIN NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'StateName')
                            ALTER TABLE Customers ADD StateName NVARCHAR(100) NOT NULL DEFAULT 'Delhi';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'StateCode')
                            ALTER TABLE Customers ADD StateCode NVARCHAR(10) NOT NULL DEFAULT '07';

                        -- Sales migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'IsGSTBill')
                            ALTER TABLE Sales ADD IsGSTBill BIT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'TaxableAmount')
                            ALTER TABLE Sales ADD TaxableAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'CGSTAmount')
                            ALTER TABLE Sales ADD CGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'SGSTAmount')
                            ALTER TABLE Sales ADD SGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'IGSTAmount')
                            ALTER TABLE Sales ADD IGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'CustomerGSTIN')
                            ALTER TABLE Sales ADD CustomerGSTIN NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'PlaceOfSupply')
                            ALTER TABLE Sales ADD PlaceOfSupply NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'IsInterState')
                            ALTER TABLE Sales ADD IsInterState BIT NOT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'CashAmount')
                            ALTER TABLE Sales ADD CashAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'OnlineAmount')
                            ALTER TABLE Sales ADD OnlineAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;

                        -- SaleDetails migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'HSNSAC')
                            ALTER TABLE SaleDetails ADD HSNSAC NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'GSTRate')
                            ALTER TABLE SaleDetails ADD GSTRate DECIMAL(18,2) NOT NULL DEFAULT 18.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'TaxableAmount')
                            ALTER TABLE SaleDetails ADD TaxableAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'CGSTAmount')
                            ALTER TABLE SaleDetails ADD CGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'SGSTAmount')
                            ALTER TABLE SaleDetails ADD SGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'IGSTAmount')
                            ALTER TABLE SaleDetails ADD IGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        -- Appointments migrations
                        ALTER TABLE Appointments ALTER COLUMN AppointmentTime NVARCHAR(100) NOT NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceStaffIds')
                            ALTER TABLE Appointments ADD ServiceStaffIds NVARCHAR(1000) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'SaleId')
                            ALTER TABLE Appointments ADD SaleId INT NULL FOREIGN KEY REFERENCES Sales(Id) ON DELETE SET NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'AppointmentId')
                            ALTER TABLE Sales ADD AppointmentId INT NULL FOREIGN KEY REFERENCES Appointments(Id) ON DELETE SET NULL;
                        -- DailySettlements migrations
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailySettlements') AND name = 'CardSales')
                            ALTER TABLE DailySettlements ADD CardSales DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailySettlements') AND name = 'QRSales')
                            ALTER TABLE DailySettlements ADD QRSales DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                    ", conn);

                    // Run chronological payments allocation migration
                    MigratePaymentsToSales();

                    // Backfill legacy Sales records to make them mathematically consistent in reports
                    ExecuteNonQuery(@"
                        UPDATE Sales 
                        SET AmountPaid = GrandTotal 
                        WHERE AmountPaid = 0.00 AND DueAmount = 0.00 AND GrandTotal > 0.00;

                        UPDATE Sales 
                        SET CashAmount = AmountPaid 
                        WHERE CashAmount = 0.00 AND OnlineAmount = 0.00 AND (PaymentMethod = 'Cash' OR PaymentMethod IS NULL);

                        UPDATE Sales 
                        SET OnlineAmount = AmountPaid 
                        WHERE CashAmount = 0.00 AND OnlineAmount = 0.00 AND (PaymentMethod IN ('Card', 'QR Pay', 'UPI', 'Wallet', 'Online'));

                        -- Auto-link legacy Billed appointments to Sales by CustomerId and Date if not already linked
                        UPDATE a
                        SET a.SaleId = s.Id
                        FROM Appointments a
                        CROSS APPLY (
                            SELECT TOP 1 Id FROM Sales 
                            WHERE CustomerId = a.CustomerId 
                              AND CAST(SaleDate AS DATE) = a.AppointmentDate 
                            ORDER BY Id DESC
                        ) s
                        WHERE a.Status = 'Billed' AND a.SaleId IS NULL;

                        UPDATE s
                        SET s.AppointmentId = a.Id
                        FROM Sales s
                        INNER JOIN Appointments a ON a.SaleId = s.Id
                        WHERE s.AppointmentId IS NULL;
                    ", conn);
                }
            }
            catch (SqlException ex)
            {
                string localDbPath = FindSqlLocalDBPath();
                if (string.IsNullOrEmpty(localDbPath))
                {
                    throw new Exception("Microsoft SQL Server LocalDB is not installed on this machine.\n\n" +
                                        "Please download and install Microsoft SQL Server LocalDB (v11.0 or newer, e.g. SQL Server 2019/2022 LocalDB) to run the application.\n" +
                                        "You can obtain the installer from Microsoft's SQL Server Express download page.\n\n" +
                                        "Error Details: " + ex.Message, ex);
                }
                else
                {
                    string diagnostics = GetLocalDBDiagnostics();
                    throw new Exception("Microsoft SQL Server LocalDB is installed, but the connection could not be established.\n\n" +
                                        "Please try resetting your LocalDB instance by running these commands in Command Prompt:\n" +
                                        "1. sqllocaldb stop MSSQLLocalDB\n" +
                                        "2. sqllocaldb delete MSSQLLocalDB\n" +
                                        "3. sqllocaldb create MSSQLLocalDB\n" +
                                        "4. sqllocaldb start MSSQLLocalDB\n" +
                                        "(Replace 'MSSQLLocalDB' with your actual instance name, such as 'v11.0', if different)\n\n" +
                                        "---------------------------------------\n" +
                                        "LOCALDB DIAGNOSTIC SYSTEM INFO:\n" +
                                        "---------------------------------------\n" +
                                        diagnostics + "\n" +
                                        "---------------------------------------\n\n" +
                                        "Error Details: " + ex.Message, ex);
                }
            }
        }

        private class SaleDueInfo
        {
            public int Id { get; set; }
            public decimal InitialDue { get; set; }
        }

        private class PaymentInfo
        {
            public int Id { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
            public string Method { get; set; }
            public string Remarks { get; set; }
            public int? User { get; set; }
        }

        public static void MigratePaymentsToSales()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Check if there are any payments without SaleId
                    string checkSql = "SELECT COUNT(*) FROM CustomerPayments WHERE SaleId IS NULL";
                    int unlinkedPayments = 0;
                    using (SqlCommand cmd = new SqlCommand(checkSql, conn))
                    {
                        unlinkedPayments = (int)cmd.ExecuteScalar();
                    }

                    if (unlinkedPayments == 0) return;

                    // Fetch all customers who have unlinked payments
                    var customerIds = new System.Collections.Generic.List<int>();
                    string getCustsSql = "SELECT DISTINCT CustomerId FROM CustomerPayments WHERE SaleId IS NULL";
                    using (SqlCommand cmd = new SqlCommand(getCustsSql, conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                customerIds.Add(rdr.GetInt32(0));
                            }
                        }
                    }

                    foreach (int custId in customerIds)
                    {
                        // Start a transaction for each customer
                        using (SqlTransaction trans = conn.BeginTransaction())
                        {
                            try
                            {
                                // Get all sales for this customer with their original due amount (GrandTotal - AmountPaid)
                                // ordered by date/id
                                var sales = new System.Collections.Generic.List<SaleDueInfo>();
                                string salesSql = "SELECT Id, GrandTotal, AmountPaid FROM Sales WHERE CustomerId = @custId ORDER BY SaleDate ASC, Id ASC";
                                using (SqlCommand cmd = new SqlCommand(salesSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@custId", custId);
                                    using (SqlDataReader rdr = cmd.ExecuteReader())
                                    {
                                        while (rdr.Read())
                                        {
                                            int saleId = rdr.GetInt32(0);
                                            decimal grand = rdr.GetDecimal(1);
                                            decimal paid = rdr.GetDecimal(2);
                                            decimal initialDue = grand - paid;
                                            if (initialDue > 0)
                                            {
                                                sales.Add(new SaleDueInfo { Id = saleId, InitialDue = initialDue });
                                            }
                                        }
                                    }
                                }

                                // Get all payments for this customer where SaleId is null
                                // ordered by payment date/id
                                var payments = new System.Collections.Generic.List<PaymentInfo>();
                                string paymentsSql = "SELECT Id, Amount, PaymentDate, PaymentMethod, Remarks, CreatedBy FROM CustomerPayments WHERE CustomerId = @custId AND SaleId IS NULL ORDER BY PaymentDate ASC, Id ASC";
                                using (SqlCommand cmd = new SqlCommand(paymentsSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@custId", custId);
                                    using (SqlDataReader rdr = cmd.ExecuteReader())
                                    {
                                        while (rdr.Read())
                                        {
                                            payments.Add(new PaymentInfo {
                                                Id = rdr.GetInt32(0),
                                                Amount = rdr.GetDecimal(1),
                                                Date = rdr.GetDateTime(2),
                                                Method = rdr.GetString(3),
                                                Remarks = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                                                User = rdr.IsDBNull(5) ? (int?)null : rdr.GetInt32(5)
                                            });
                                        }
                                    }
                                }

                                // Match payments to sales
                                int saleIdx = 0;
                                foreach (var pay in payments)
                                {
                                    decimal remainingPay = pay.Amount;
                                    bool isFirstAlloc = true;

                                    while (remainingPay > 0 && saleIdx < sales.Count)
                                    {
                                        var activeSale = sales[saleIdx];
                                        
                                        // Load how much has been allocated to this sale so far from database
                                        decimal allocatedSoFar = 0;
                                        string getAllocSql = "SELECT ISNULL(SUM(Amount), 0) FROM CustomerPayments WHERE SaleId = @saleId";
                                        using (SqlCommand cmd = new SqlCommand(getAllocSql, conn, trans))
                                        {
                                            cmd.Parameters.AddWithValue("@saleId", activeSale.Id);
                                            allocatedSoFar = Convert.ToDecimal(cmd.ExecuteScalar());
                                        }

                                        decimal remainingDue = activeSale.InitialDue - allocatedSoFar;
                                        if (remainingDue <= 0)
                                        {
                                            saleIdx++;
                                            continue;
                                        }

                                        decimal alloc = Math.Min(remainingPay, remainingDue);
                                        
                                        if (isFirstAlloc)
                                        {
                                            // Update the first matching record in CustomerPayments
                                            string updatePaySql = "UPDATE CustomerPayments SET SaleId = @saleId, Amount = @amount WHERE Id = @payId";
                                            using (SqlCommand cmd = new SqlCommand(updatePaySql, conn, trans))
                                            {
                                                cmd.Parameters.AddWithValue("@saleId", activeSale.Id);
                                                cmd.Parameters.AddWithValue("@amount", alloc);
                                                cmd.Parameters.AddWithValue("@payId", pay.Id);
                                                cmd.ExecuteNonQuery();
                                            }
                                            isFirstAlloc = false;
                                        }
                                        else
                                        {
                                            // Insert a split payment record for the remainder
                                            string insertPaySql = @"
                                                INSERT INTO CustomerPayments (CustomerId, PaymentDate, Amount, PaymentMethod, Remarks, CreatedBy, SaleId)
                                                VALUES (@custId, @date, @amount, @method, @remarks, @user, @saleId)";
                                            using (SqlCommand cmd = new SqlCommand(insertPaySql, conn, trans))
                                            {
                                                cmd.Parameters.AddWithValue("@custId", custId);
                                                cmd.Parameters.AddWithValue("@date", pay.Date);
                                                cmd.Parameters.AddWithValue("@amount", alloc);
                                                cmd.Parameters.AddWithValue("@method", pay.Method);
                                                cmd.Parameters.AddWithValue("@remarks", pay.Remarks);
                                                cmd.Parameters.AddWithValue("@user", (object)pay.User ?? DBNull.Value);
                                                cmd.Parameters.AddWithValue("@saleId", activeSale.Id);
                                                cmd.ExecuteNonQuery();
                                            }
                                        }

                                        remainingPay -= alloc;
                                    }

                                    // If there is still payment left over after matching all sales (overpayment)
                                    if (remainingPay > 0)
                                    {
                                        if (isFirstAlloc)
                                        {
                                            // It remains unlinked (SaleId = null)
                                            string updatePaySql = "UPDATE CustomerPayments SET SaleId = NULL, Amount = @amount WHERE Id = @payId";
                                            using (SqlCommand cmd = new SqlCommand(updatePaySql, conn, trans))
                                            {
                                                cmd.Parameters.AddWithValue("@amount", remainingPay);
                                                cmd.Parameters.AddWithValue("@payId", pay.Id);
                                                cmd.ExecuteNonQuery();
                                            }
                                        }
                                        else
                                        {
                                            // Insert split payment record with null SaleId
                                            string insertPaySql = @"
                                                INSERT INTO CustomerPayments (CustomerId, PaymentDate, Amount, PaymentMethod, Remarks, CreatedBy, SaleId)
                                                VALUES (@custId, @date, @amount, @method, @remarks, @user, NULL)";
                                            using (SqlCommand cmd = new SqlCommand(insertPaySql, conn, trans))
                                            {
                                                cmd.Parameters.AddWithValue("@custId", custId);
                                                cmd.Parameters.AddWithValue("@date", pay.Date);
                                                cmd.Parameters.AddWithValue("@amount", remainingPay);
                                                cmd.Parameters.AddWithValue("@method", pay.Method);
                                                cmd.Parameters.AddWithValue("@remarks", pay.Remarks);
                                                cmd.Parameters.AddWithValue("@user", (object)pay.User ?? DBNull.Value);
                                                cmd.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }

                                trans.Commit();
                            }
                            catch (Exception ex)
                            {
                                trans.Rollback();
                                System.Diagnostics.Debug.WriteLine("Customer transaction failed: " + ex.Message);
                                throw;
                            }
                        }
                    }
                    // ========================================================
                    // INDIAN GST SCHEMA MIGRATIONS (Non-Breaking)
                    // ========================================================
                    // 1. AppProfile GST fields
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'StateName')
                            ALTER TABLE AppProfile ADD StateName NVARCHAR(100) NOT NULL DEFAULT 'Delhi';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'StateCode')
                            ALTER TABLE AppProfile ADD StateCode NVARCHAR(10) NOT NULL DEFAULT '07';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'IsTaxInclusive')
                            ALTER TABLE AppProfile ADD IsTaxInclusive BIT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'DefaultBillType')
                            ALTER TABLE AppProfile ADD DefaultBillType NVARCHAR(20) NOT NULL DEFAULT 'GST';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AppProfile') AND name = 'DefaultGSTRate')
                            ALTER TABLE AppProfile ADD DefaultGSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00;
                    ", conn);

                    // 2. Services SAC & GST Slab
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Services') AND name = 'SACCode')
                            ALTER TABLE Services ADD SACCode NVARCHAR(20) NOT NULL DEFAULT '999721';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Services') AND name = 'GSTRate')
                            ALTER TABLE Services ADD GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00;
                    ", conn);

                    // 3. Products HSN & GST Slab
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'HSNCode')
                            ALTER TABLE Products ADD HSNCode NVARCHAR(20) NOT NULL DEFAULT '3305';
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'GSTRate')
                            ALTER TABLE Products ADD GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00;
                    ", conn);

                    // 4. Customers GSTIN & State
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'GSTIN')
                            ALTER TABLE Customers ADD GSTIN NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'StateName')
                            ALTER TABLE Customers ADD StateName NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'StateCode')
                            ALTER TABLE Customers ADD StateCode NVARCHAR(10) NULL;
                    ", conn);

                    // 5. Sales GST Breakdown Fields
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'IsGSTBill')
                            ALTER TABLE Sales ADD IsGSTBill BIT NOT NULL DEFAULT 1;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'TaxableAmount')
                            ALTER TABLE Sales ADD TaxableAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'CGSTAmount')
                            ALTER TABLE Sales ADD CGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'SGSTAmount')
                            ALTER TABLE Sales ADD SGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'IGSTAmount')
                            ALTER TABLE Sales ADD IGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'CustomerGSTIN')
                            ALTER TABLE Sales ADD CustomerGSTIN NVARCHAR(50) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'PlaceOfSupply')
                            ALTER TABLE Sales ADD PlaceOfSupply NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'IsInterState')
                            ALTER TABLE Sales ADD IsInterState BIT NOT NULL DEFAULT 0;
                    ", conn);

                    // 6. SaleDetails Line-Item GST Fields
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'HSNSAC')
                            ALTER TABLE SaleDetails ADD HSNSAC NVARCHAR(20) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'GSTRate')
                            ALTER TABLE SaleDetails ADD GSTRate DECIMAL(5,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'TaxableAmount')
                            ALTER TABLE SaleDetails ADD TaxableAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'CGSTAmount')
                            ALTER TABLE SaleDetails ADD CGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'SGSTAmount')
                            ALTER TABLE SaleDetails ADD SGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SaleDetails') AND name = 'IGSTAmount')
                            ALTER TABLE SaleDetails ADD IGSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
                    ", conn);

                    // 7. HsnSacMaster table check & seed
                    ExecuteNonQuery(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HsnSacMaster')
                        BEGIN
                            CREATE TABLE HsnSacMaster (
                                Id INT PRIMARY KEY IDENTITY(1,1),
                                Code NVARCHAR(50) NOT NULL UNIQUE,
                                Type NVARCHAR(20) NOT NULL DEFAULT 'HSN',
                                Description NVARCHAR(500) NOT NULL,
                                GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00,
                                IsActive BIT NOT NULL DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            );

                            INSERT INTO HsnSacMaster (Code, Type, Description, GSTRate, IsActive) VALUES 
                            ('999721', 'SAC', 'Hairdressing and barbers services (haircut, hair wash, blowdry, styling, beard trim, hair color)', 18.00, 1),
                            ('999722', 'SAC', 'Cosmetic and beauty treatment services including manicure, pedicure, facial, waxing, makeup', 18.00, 1),
                            ('999729', 'SAC', 'Other beauty and wellness treatment services including spa therapies, sauna, relaxing massage', 18.00, 1),
                            ('999723', 'SAC', 'Physical well-being services and body treatments', 18.00, 1),
                            ('998399', 'SAC', 'Other professional, technical and business salon consulting services', 18.00, 1),
                            ('3305', 'HSN', 'Preparations for use on hair: shampoos, hair creams, dyes, bleaches, styling sprays, hair oils', 18.00, 1),
                            ('3304', 'HSN', 'Beauty or make-up preparations & skin care: face creams, serums, lotions, cleansers, manicures/pedicures', 18.00, 1),
                            ('3307', 'HSN', 'Pre-shave, shaving or after-shave preparations, personal deodorants, bath preparations', 18.00, 1),
                            ('3303', 'HSN', 'Perfumes and toilet waters / body sprays', 18.00, 1),
                            ('3401', 'HSN', 'Soap, organic surface-active products and preparations for washing the skin', 18.00, 1),
                            ('8214', 'HSN', 'Hair clippers, razors, scissors, manicure/pedicure sets and instruments', 18.00, 1),
                            ('8516', 'HSN', 'Electro-thermic hair dressing appliances: hair dryers, hair straighteners, hair curling tongs', 18.00, 1),
                            ('9615', 'HSN', 'Combs, hair-slides, hairpins, hair curlers and salon styling accessories', 18.00, 1),
                            ('3004', 'HSN', 'Medicament preparations, antiseptic lotions and medicated skin care ointments', 12.00, 1),
                            ('4818', 'HSN', 'Sanitary salon paper towels, facial tissues, neck strips, disposable wipes', 18.00, 1);
                        END
                    ", conn);

                    // Categories Schema Migration (Separated batches for SQL Server compiler)
                    ExecuteNonQuery("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Type') ALTER TABLE Categories ADD Type NVARCHAR(20) NOT NULL DEFAULT 'Service';", conn);
                    ExecuteNonQuery("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'HsnSacCode') ALTER TABLE Categories ADD HsnSacCode NVARCHAR(50) NULL DEFAULT '999721';", conn);
                    ExecuteNonQuery("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'GSTRate') ALTER TABLE Categories ADD GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00;", conn);

                    ExecuteNonQuery(@"
                        UPDATE Categories SET Type = 'Service', HsnSacCode = '999721', GSTRate = 18.00 WHERE Name = 'Hair Services' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Service', HsnSacCode = '999721', GSTRate = 18.00 WHERE Name = 'Beard & Grooming' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Service', HsnSacCode = '999722', GSTRate = 18.00 WHERE Name = 'Facial & Skin Care' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Service', HsnSacCode = '999721', GSTRate = 18.00 WHERE Name = 'Hair Spa & Treatments' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Service', HsnSacCode = '999729', GSTRate = 18.00 WHERE Name = 'Body Massage & Spa' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Service', HsnSacCode = '999722', GSTRate = 18.00 WHERE Name = 'Manicure & Pedicure' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Product', HsnSacCode = '3305', GSTRate = 18.00 WHERE Name = 'Hair Care Products' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Product', HsnSacCode = '3304', GSTRate = 18.00 WHERE Name = 'Skin Care Products' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                        UPDATE Categories SET Type = 'Product', HsnSacCode = '8214', GSTRate = 18.00 WHERE Name = 'Grooming Accessories' AND (HsnSacCode IS NULL OR HsnSacCode = '');
                    ", conn);

                    // Appointments Multi-Service Migration
                    ExecuteNonQuery("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceIds') ALTER TABLE Appointments ADD ServiceIds NVARCHAR(500) NULL;", conn);
                    ExecuteNonQuery("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceNames') ALTER TABLE Appointments ADD ServiceNames NVARCHAR(1000) NULL;", conn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Migration failed: " + ex.Message);
            }
        }

        private static void ExecuteNonQuery(string sql, SqlConnection conn)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }

    public class GSTState
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public override string ToString() => $"{Code} - {Name}";
    }

    public static class IndianGSTHelper
    {
        public static System.Collections.Generic.List<GSTState> GetIndianStates()
        {
            return new System.Collections.Generic.List<GSTState>
            {
                new GSTState { Code = "01", Name = "Jammu and Kashmir" },
                new GSTState { Code = "02", Name = "Himachal Pradesh" },
                new GSTState { Code = "03", Name = "Punjab" },
                new GSTState { Code = "04", Name = "Chandigarh" },
                new GSTState { Code = "05", Name = "Uttarakhand" },
                new GSTState { Code = "06", Name = "Haryana" },
                new GSTState { Code = "07", Name = "Delhi" },
                new GSTState { Code = "08", Name = "Rajasthan" },
                new GSTState { Code = "09", Name = "Uttar Pradesh" },
                new GSTState { Code = "10", Name = "Bihar" },
                new GSTState { Code = "11", Name = "Sikkim" },
                new GSTState { Code = "12", Name = "Arunachal Pradesh" },
                new GSTState { Code = "13", Name = "Nagaland" },
                new GSTState { Code = "14", Name = "Manipur" },
                new GSTState { Code = "15", Name = "Mizoram" },
                new GSTState { Code = "16", Name = "Tripura" },
                new GSTState { Code = "17", Name = "Meghalaya" },
                new GSTState { Code = "18", Name = "Assam" },
                new GSTState { Code = "19", Name = "West Bengal" },
                new GSTState { Code = "20", Name = "Jharkhand" },
                new GSTState { Code = "21", Name = "Odisha" },
                new GSTState { Code = "22", Name = "Chhattisgarh" },
                new GSTState { Code = "23", Name = "Madhya Pradesh" },
                new GSTState { Code = "24", Name = "Gujarat" },
                new GSTState { Code = "26", Name = "Dadra & Nagar Haveli and Daman & Diu" },
                new GSTState { Code = "27", Name = "Maharashtra" },
                new GSTState { Code = "29", Name = "Karnataka" },
                new GSTState { Code = "30", Name = "Goa" },
                new GSTState { Code = "31", Name = "Lakshadweep" },
                new GSTState { Code = "32", Name = "Kerala" },
                new GSTState { Code = "33", Name = "Tamil Nadu" },
                new GSTState { Code = "34", Name = "Puducherry" },
                new GSTState { Code = "35", Name = "Andaman and Nicobar Islands" },
                new GSTState { Code = "36", Name = "Telangana" },
                new GSTState { Code = "37", Name = "Andhra Pradesh" },
                new GSTState { Code = "38", Name = "Ladakh" },
                new GSTState { Code = "97", Name = "Other Territory" }
            };
        }

        public static string AmountToWords(decimal amount)
        {
            if (amount == 0) return "Rupees Zero Only";
            if (amount < 0) return "Minus " + AmountToWords(Math.Abs(amount));

            long wholePart = (long)Math.Truncate(amount);
            int paisePart = (int)Math.Round((amount - wholePart) * 100);

            string words = "Rupees " + ConvertNumberToWords(wholePart);
            if (paisePart > 0)
            {
                words += " and " + ConvertNumberToWords(paisePart) + " Paise";
            }
            words += " Only";
            return words;
        }

        private static string ConvertNumberToWords(long number)
        {
            if (number == 0) return "Zero";

            string[] unitsMap = { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            string[] tensMap = { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            string words = "";

            if ((number / 10000000) > 0)
            {
                words += ConvertNumberToWords(number / 10000000) + " Crore ";
                number %= 10000000;
            }

            if ((number / 100000) > 0)
            {
                words += ConvertNumberToWords(number / 100000) + " Lakh ";
                number %= 100000;
            }

            if ((number / 1000) > 0)
            {
                words += ConvertNumberToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += ConvertNumberToWords(number / 100) + " Hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += " " + unitsMap[number % 10];
                }
            }

            return words.Trim();
        }
    }
}
