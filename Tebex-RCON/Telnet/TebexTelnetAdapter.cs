using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Tebex.Adapters;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON;
using Tebex.Triage;

namespace Tebex_RCON;

public class TebexTelnetAdapter : BaseTebexAdapter
{
        private TextWriter Logger;
        public TebexTelnetClient Telnet;
        private TebexTelnetPlugin Plugin;
        private const string ConfigFilePath = "tebex-config.json";
        public static readonly string Version = "1.0.0-alpha.1";
        private static bool IsReady = false;

        public override void Init()
        {
            // Setup log
            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var logName = $"TebexTelnet-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.log";
            Logger = new StreamWriter(logName, true);
            LogInfo($"Log file is being saved to '{currentPath}\\{logName}'");
            
            LogInfo($"Tebex RCON Adapter Client [TELNET] {Version} | https://tebex.io/");
            
            // Create/read config
            if (File.Exists(ConfigFilePath))
            {
                // Read existing config file
                string jsonText = File.ReadAllText(ConfigFilePath);
                PluginConfig = JsonConvert.DeserializeObject<TebexConfig>(jsonText);
            }
            else
            {
                // Create new config file with default settings
                PluginConfig = new TebexConfig();
                SaveConfig();
            }
            
            // Check store secret key
            if (PluginConfig.SecretKey != "your-secret-key-here" && PluginConfig.SecretKey != "")
            {
                Console.WriteLine();
                LogInfo("Webstore key is already configured, we will verify your key and connect to the game server.");
                FetchStoreInfo(info =>
                {
                    LogInfo($" > '{info.ServerInfo.Name}' '{info.AccountInfo.Name}'");
                    
                    // Connect to RCON after secret key is verified correct
                    LogInfo($"Connecting to game server at {PluginConfig.RconIp}:{PluginConfig.RconPort}...");
                    Telnet = new TebexTelnetClient();
                    var connectTask = Telnet.ConnectAsync(PluginConfig.RconIp, PluginConfig.RconPort,
                        PluginConfig.RconPassword, true);
                    connectTask.Wait();
                    
                    if (!connectTask.Result)
                    {
                        LogError($"> Could not connect to your server. Please check your IP and port, and try again.");
                        return;
                    }
                    
                    LogInfo(" > Successful");
                    
                    LogInfo($"Configuring adapter for ${info.AccountInfo.GameType}");
                    Plugin = new SevenDaysPlugin(Telnet, this);
                    
                    // Ensure the game at the RCON endpoint is the one for this account
                    if (!Plugin.AuthenticateGame(info.AccountInfo.GameType))
                    {
                        LogError($" > It does not appear that the server we connected to is a {info.AccountInfo.GameType} server");
                        LogError($" > Please check your secret key and store settings.");
                        Environment.Exit(1);
                        return;
                    }
                    LogInfo(" > Successful");
                    
                    /* TODO allow plugin to decide if it reads rcon
                    Thread readThread = new Thread(Rcon.ReadRconMessages);
                    readThread.Start();
                    */
                    
                    IsReady = true;
                }, (tebexError) =>
                {
                    LogWarning("Tebex is not running.");
                }, (code, response) =>
                {
                    LogError($"An unexpected server error occurred with code {code}: {response}");
                    LogWarning("Tebex is not running.");
                });
            }
            else //Secret key is not set in some way.
            {
                DoSetup();
            }
            
            // Setup timed functions
            ExecuteEvery(TimeSpan.FromSeconds(121), () =>
            {
                ProcessCommandQueue(false);
            });
            
            ExecuteEvery(TimeSpan.FromSeconds(61), () =>
            {
                DeleteExecutedCommands(false);
            });
            
            ExecuteEvery(TimeSpan.FromSeconds(61), () =>
            {
                ProcessJoinQueue(false);
            });
            
            /*
            ExecuteEvery(TimeSpan.FromMinutes(30), () =>
            {
                RefreshStoreInformation(false);
            });*/
            
            Console.WriteLine("Type 'tebex.help' for a list of commands.");
            Console.WriteLine("");
        }
    
        public void DoSetup()
        {
            LogInfo("Starting Tebex setup...");
            Console.WriteLine("> Add your game server at https://creator.tebex.io/game-servers/ to get your key.");
            SetupSecretKey();
        }
        
        public void SetupSecretKey()
        {
            var oldKey = PluginConfig.SecretKey;
            var secretKey = "";
            
            Console.Write("Enter store secret key: ");
            secretKey = Console.ReadLine();
            if (string.IsNullOrEmpty(secretKey))
            {
                SetupSecretKey();
            }

            PluginConfig.SecretKey = secretKey;
            Console.WriteLine("> Verifying secret key...");
            FetchStoreInfo(info =>
            {
                Console.WriteLine("> Secret key set successfully");
                Console.WriteLine();
                SaveConfig();
                SetupTelnetConnection();
            }, error =>
            {
                LogInfo($"> An error occurred: {error.ErrorMessage}");
                PluginConfig.SecretKey = oldKey;
                SaveConfig();
                Environment.Exit(1);
            }, (code, message) =>
            {
                LogError($"> Encountered server error: {message}. Please try again.");
                PluginConfig.SecretKey = oldKey;
                SaveConfig();
                Environment.Exit(1);
            });
        }
        
        public void SetupTelnetConnection()
        {
            while (true)
            {
                Console.Write("Enter server Telnet IP (empty to skip): ");
                var serverIp = Console.ReadLine();
                if (String.IsNullOrEmpty(serverIp))
                {
                    break;
                }

                IPAddress parsedIp;
                if (!IPAddress.TryParse(serverIp, out parsedIp))
                {
                    Console.WriteLine("> Invalid IP address. Please enter a valid IP address.");
                    continue;
                }

                PluginConfig.RconIp = parsedIp.ToString();
                SaveConfig();
                break;
            }

            while (true)
            {
                Console.Write("Enter server Telnet port (empty to skip): ");
                var rconPort = Console.ReadLine();
                var rconIntPort = 0;
                if (String.IsNullOrEmpty(rconPort))
                {
                    break;
                }

                if (!int.TryParse(rconPort, out rconIntPort))
                {
                    Console.WriteLine("> Invalid port. Must be a number between 1 - 65535.");
                    continue;
                }

                if (rconIntPort > 65535 || rconIntPort < 1)
                {
                    Console.WriteLine("> Invalid range. Must be between 1 - 65535.");
                    continue;
                }

                PluginConfig.RconPort = rconIntPort;
                SaveConfig();
                break;
            }
            
            Console.Write("Enter the Telnet password (enter to skip): ");
            var rconPassword = Console.ReadLine();
            if (string.IsNullOrEmpty(rconPassword.ToString()))
            {
                PluginConfig.RconPassword = "";
                LogWarning("> WARNING! It is insecure to use RCON without a password.");
            }
            
            PluginConfig.RconPassword = rconPassword.ToString();
            SaveConfig();
            LogInfo($"> Checking connection to the server at {PluginConfig.RconIp}:{PluginConfig.RconPort}...");
            
            var client = new TebexRconClient(PluginConfig.RconIp, PluginConfig.RconPort, PluginConfig.RconPassword, false);
            var (success, error) = client.Connect();
            if (error != null)
            {
                LogError($"> An error occurred while connecting to RCON: {error.Message}");
                return;
            }
            
            if (!success)
            {
                LogError("> Failed to connect to that server. Please double check that it is online, and try again.");
            }
            else
            {
                LogInfo("> Connection successful. Tebex is now set up properly.");
                LogInfo("Please leave this application running in order for Tebex commands to be processed.");
            }
        }
        
        #region Threading
        public static async void ExecuteEvery(TimeSpan interval, Action action)
        {
            if (!IsReady)
            {
                return;
            }
            
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                action();

                try
                {
                    await Task.Delay(interval, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public static async void ExecuteOnce(int delaySeconds, Action action)
        {
            await Task.Delay(delaySeconds);
            action();
        }
        
        #endregion
        
    public void SaveConfig()
    {
        string jsonText = JsonConvert.SerializeObject(PluginConfig, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, jsonText);
    }
    
    private enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }
    
    private void Log(string message, LogLevel level)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logMessage = $"[{timestamp}] [{level}] {message}";
        Console.WriteLine($"{message}");
        Logger.WriteLine(logMessage);
        Logger.Flush();
    }
    
    public override void LogWarning(string message)
    {
        Log(message, LogLevel.Warning);
    }

    public override void LogError(string message)
    {
        Log(message, LogLevel.Error);
    }

    public override void LogInfo(string message)
    {
        Log(message, LogLevel.Info);
    }

    public override void LogDebug(string message)
    {
        if (PluginConfig.DebugMode)
        {
            Log(message, LogLevel.Debug);    
        }
    }

    public override void ReplyPlayer(object player, string message)
    {
        throw new NotImplementedException();
    }

    public override void ExecuteOfflineCommand(TebexApi.Command command, string commandName, string[] args)
    {
        throw new NotImplementedException();
    }

    public override void ExecuteOnlineCommand(TebexApi.Command command, object playerObj, string commandName, string[] args)
    {
        throw new NotImplementedException();
    }

    public override bool IsPlayerOnline(string playerRefId)
    {
        throw new NotImplementedException();
    }

    public override object GetPlayerRef(string playerId)
    {
        throw new NotImplementedException();
    }

    public override string ExpandUsernameVariables(string input, object playerObj)
    {
        throw new NotImplementedException();
    }

    public override string ExpandOfflineVariables(string input, TebexApi.PlayerInfo info)
    {
        throw new NotImplementedException();
    }

    public override void MakeWebRequest(string url, string body, TebexApi.HttpVerb verb,
        TebexApi.ApiSuccessCallback onSuccess,
        TebexApi.ApiErrorCallback onApiError, TebexApi.ServerErrorCallback onServerError)
    {
        try
        {
            using (HttpClient httpClient = new HttpClient())
            {
                LogDebug($" -> {verb.ToString()} {url} | {body}");

                // Set request headers
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"TebexRconAdapter");
                httpClient.DefaultRequestHeaders.Add("X-Tebex-Secret", PluginConfig.SecretKey);

                StringContent data = new StringContent(body, Encoding.UTF8, "application/json");
                HttpResponseMessage response = null;

                Task<HttpResponseMessage> task = null;
                switch (verb)
                {
                    case TebexApi.HttpVerb.GET:
                        task = httpClient.GetAsync(url);
                        break;

                    case TebexApi.HttpVerb.POST:
                        task = httpClient.PostAsync(url, data);
                        break;

                    case TebexApi.HttpVerb.PUT:
                        task = httpClient.PutAsync(url, data);
                        break;

                    case TebexApi.HttpVerb.DELETE:
                        HttpRequestMessage request = new HttpRequestMessage();
                        request.RequestUri = new Uri(url);
                        request.Content = data;
                        request.Method = HttpMethod.Delete;
                        task = httpClient.SendAsync(request);
                        break;
                }

                task.Wait();
                response = task.Result;

                var code = (int)response.StatusCode;
                LogDebug($"{code} <- {verb.ToString()} {url}");

                Task<String> readTask = response.Content.ReadAsStringAsync();
                readTask.Wait();
                string content = readTask.Result;
                LogDebug($" | {content}");

                if (response.IsSuccessStatusCode)
                {
                    onSuccess?.Invoke(code, content);
                }
                else if (code >= 400 && code <= 499)
                {
                    var tebexError = JsonConvert.DeserializeObject<TebexApi.TebexError>(content);
                    onApiError?.Invoke(tebexError);
                }
                else if (code >= 500)
                {
                    onServerError?.Invoke(code, content);
                }
            }
        }
        catch (Exception ex)
        {
            onServerError?.Invoke(0, ex.Message);
        }
    }

    public override TebexTriage.AutoTriageEvent FillAutoTriageParameters(TebexTriage.AutoTriageEvent partialEvent)
    {
        throw new NotImplementedException();
    }
}