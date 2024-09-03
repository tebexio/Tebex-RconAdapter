using System.Text;
using Newtonsoft.Json;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON.Protocol;
using Tebex.Triage;
using Tebex.Util;

namespace Tebex.Adapters
{
    /** Provides logic that implements some RCON protocol */
    public class TebexRconAdapter : BaseTebexAdapter
    {
        public Dictionary<string, Type> PLUGINS = new Dictionary<string, Type>()
        {
            {"Minecraft: Java Edition", typeof(MinecraftPlugin)},
            {"ARK: Survival Evolved", typeof(ArkPlugin)},
            {"Rust", typeof(RustPlugin)},
            {"7 Days To Die", typeof(SevenDaysPlugin)},
            {"Conan Exiles", typeof(ConanExilesPlugin)},
        };
        
        public const string Version = "1.1.0";
        private const string ConfigFilePath = "./tebex-config.json";

        private Type? _pluginType;
        private RconPlugin? _plugin;
        //protected static ProtocolManagerBase? Protocol;
        private RconConnection? _rcon;
        private TextWriter? _logger;
        private static bool _isReady = false;

        private TebexConfig? _startupConfig;

        private TebexConfig ReadConfig()
        {
            var cfg = new TebexConfig();
            
            // Read or create the config file
            if (File.Exists(ConfigFilePath))
            {
                string jsonText = File.ReadAllText(ConfigFilePath);
                cfg = JsonConvert.DeserializeObject<TebexConfig>(jsonText);
            }
            else
            {
                cfg = new TebexConfig(); // default settings are applied
                SaveConfig(cfg);
            }

            return cfg;
        }

        public RconConnection GetRcon()
        {
            return _rcon;
        }

        public override bool IsConnected()
        {
            return _isReady;
        }
        
        public string Success(String message)
        {
            return $"[ {Ansi.Green("\u2713")} ] " + message;
        }

        public string Error(String message)
        {
            return $"[ {Ansi.Red("X")} ] " + message;
        }

        public string Warn(String message)
        {
            return $"[ {Ansi.Yellow("\u26a0")} ] " + message;
        }
        
        public override void Init()
        {
            // Setup log
            var currentPath = AppContext.BaseDirectory;
            char pathSeparator = Path.PathSeparator;
            if (pathSeparator == ':')
            {
                pathSeparator = '/';
            }
            
            var logName = $"TebexRcon-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.log";
            var logPath = currentPath + pathSeparator + logName;
            _logger = new StreamWriter(logPath, true);
            LogInfo(Ansi.White($"Log file is being saved to '{currentPath}{pathSeparator}{logName}'"));
            LogInfo($"{Ansi.Yellow("Tebex RCON Adapter Client " + Version)} | {Ansi.Blue(Ansi.Underline("https://tebex.io"))}");
            
            // This will be set if we have enough vars from either startup arguments or environment variables
            // for the adapter to attempt to start.
            if (_startupConfig == null)
            {
                // When we don't have enough startup args, assume the user is launching directly
                // and will want a setup prompt.
                PluginConfig = ReadConfig();
            }
            else // Startup config has been provided prior to adapter init, such as command line args or env vars,
                 // this skips configured params in config file
            {
                PluginConfig = _startupConfig;
            }

            // If no secret key is set, assume we need to run setup and collect info from the user.
            if (PluginConfig.SecretKey == "")
            {
                LogWarning("Your webstore's secret key has not been configured yet.", "Initiating setup...");
                DoSetup();
            }

            FetchStoreInfo(info =>
            {
                LogInfo(Success($"Validated Tebex store {Ansi.Blue(info.AccountInfo.Name)} running game type {Ansi.Blue(info.AccountInfo.GameType)}"));
                
                String gameType = info.AccountInfo.GameType;
                
                // Attempt to create plugin
                try
                {
                    _plugin = Activator.CreateInstance(PLUGINS[gameType], this) as RconPlugin;
                }
                catch (Exception e)
                {
                    Error(e.Message);
                }

                if (_plugin == null)
                {
                    LogWarning(Warn($"We do not support enhanced RCON for '{gameType}'."),
                        "Commands will be sent, but game-specific features such as players online will be disabled.");
                }
                else
                {
                    LogInfo(Success("Loaded Tebex RCON plugin for " + gameType + ". Enhanced RCON support is enabled."));
                }
                
                
                // Connect to RCON after secret key is verified correct
                try
                {
                    if (_plugin != null)
                    {
                        _rcon = _plugin.CreateRconConnection(this, PluginConfig.RconIp, PluginConfig.RconPort, PluginConfig.RconPassword);                        
                    }
                    else
                    {
                        _rcon = new RconConnection(this, PluginConfig.RconIp, PluginConfig.RconPort,
                            PluginConfig.RconPassword);
                    }
                    
                    LogInfo($"Connecting to RCON server at {PluginConfig.RconIp}:{PluginConfig.RconPort} using {_rcon.GetType().Name}...");
                    Tuple<bool, string> connectResult = _rcon.Connect();
                    if (!connectResult.Item1)
                    {
                        LogError(Error($"{connectResult.Item2}. Check that your RCON connection parameters are correct, and try again."));
                        Environment.Exit(1);
                        return;
                    }

                    // Setup timed functions
                    ExecuteEvery(TimeSpan.FromSeconds(45), () =>
                    { 
                        ProcessCommandQueue(false);
                    });
            
                    ExecuteEvery(TimeSpan.FromSeconds(45), () =>
                    {
                        DeleteExecutedCommands(false);
                    });
            
                    ExecuteEvery(TimeSpan.FromSeconds(45), () =>
                    {
                        ProcessJoinQueue(false);
                    });
                }
                catch (Exception e)
                {
                    LogError(Error("Could not connect to game server RCON: " + e));
                    return;
                }
                
                LogInfo(Success("Connected to RCON server at " + PluginConfig.RconIp + ":" + PluginConfig.RconPort));
                LogInfo(Success("Tebex is ready. Packages will automatically be delivered to your game server. We will attempt to reconnect if the server goes offline."));

                Console.WriteLine("Enter 'tebex.help' for a list of available commands. Non-Tebex commands will be passed to your game server.");
                _isReady = true;
            }, error =>
            {
                LogError(Error("Failed to connect to Tebex: " + error.ErrorMessage));
                LogInfo("Use `tebex.secret` to set your secret key.");
            });
        }

        public void SetStartupArguments(string key, string host, string port, string auth, string debug)
        {
            TebexConfig newStartupConfig = new TebexConfig();
            
            if (key != "")
            {
                newStartupConfig.SecretKey = key;
            }

            if (host != "")
            {
                newStartupConfig.RconIp = host;
            }

            if (port != "")
            {
                newStartupConfig.RconPort = int.Parse(port);
            }

            if (auth != "")
            {
                newStartupConfig.RconPassword = auth;
            }

            if (debug != "")
            {
                newStartupConfig.DebugMode = Boolean.Parse(debug);
            }
            
            // Determine if we were provided the minimum required to start
            if (newStartupConfig.SecretKey != ""
                && newStartupConfig.RconIp != ""
                && newStartupConfig.RconPort != 0)
            {
                // Read or create the config file and pass other configured elements through
                // to the startup config
                var fileConfig = ReadConfig();
                _startupConfig = newStartupConfig;

                _startupConfig.CacheLifetime = fileConfig.CacheLifetime;
                _startupConfig.AutoReportingEnabled = fileConfig.AutoReportingEnabled;

                // If debug mode wasn't requested from env or command line, ensure we read
                // the set value from the config file
                if (!newStartupConfig.DebugMode)
                {
                    _startupConfig.DebugMode = fileConfig.DebugMode;    
                }
            }
        }
        
        // public ProtocolManagerBase? GetProtocol()
        // {
        //     return Protocol;
        // }
        //
        // public void SetProtocol(ProtocolManagerBase protocol)
        // {
        //     LogDebug($"Using {protocol.GetProtocolName()} protocol");
        //     Protocol = protocol;
        // }

        // public TebexRconPlugin GetPlugin()
        // {
        //     return _plugin;
        // }
        //
        // public void SetPluginType(Type type)
        // {
        //     _pluginType = type;
        // }
        
        // public void InitPlugin(TebexRconPlugin plugin)
        // {
        //     LogDebug($"Configuring {plugin.GetGameName()} plugin {plugin.GetPluginVersion()}");
        //     _plugin = plugin;
        // }
        
        public override void SaveConfig(TebexConfig config)
        {
            string jsonText = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, jsonText);
        }

        public override void ReplyPlayer(object player, string message)
        {
            throw new NotImplementedException();
        }

        public override void ExecuteOfflineCommand(TebexApi.Command command, object playerObj, string commandName, string[] args)
        {
            var rconCommand = command.CommandToRun;
            ExpandOfflineVariables(rconCommand, command.Player);
            
            if (command.Conditions.Delay > 0)
            {
                // Command requires a delay, use built-in plugin timer to wait until callback
                // in order to respect game threads
                LogDebug($"Delaying offine command for {command.Conditions.Delay} seconds due to configuration: {rconCommand}");
                ExecuteOnce(command.Conditions.Delay,
                    () =>
                    {
                        var req = _rcon.Send(rconCommand);
                        var response = _rcon.ReceiveResponseTo(req.Id, 10);
                        LogDebug("delayed offline command request: " + req);
                        LogDebug("delayed offline command response: {} " + response);
                    });
            }
            else // No delay, execute immediately
            {
                var req = _rcon.Send(rconCommand);
                var response = _rcon.ReceiveNext();
                LogDebug("offline command request: " + req);
                LogDebug("offline command response: {} " + response);
            }
        }

        public override bool ExecuteOnlineCommand(TebexApi.Command command, TebexApi.DuePlayer player, string commandName, string[] args)
        {
            var cmd = ExpandUsernameVariables(command.CommandToRun, player);
            cmd = _plugin.ExpandGameUsernameVariables(cmd, player);
            
            LogInfo($"> Executing online command: {cmd}");
            var req = _rcon.Send(cmd);
            var response = _rcon.ReceiveResponseTo(req.Id, 10);
            if (!response.Item2.Equals("")) // error message in response pair
            {
                LogError("Failed to run online command: " + response.Item2);
                return false;
            }
            else
            {
                LogInfo($"> Server responded: '{response.Item1.Response}'");    
            }
            
            // loosely attempt to determine if we succeeded
            var lowerResponse = response.Item1.Response.Message.ToLower();
            if (lowerResponse.Contains("error") || lowerResponse.Contains("invalid") || lowerResponse.Contains("unknown item") || lowerResponse.Contains("failed"))
            {
                return false;
            }
            
            return true; // successful command
        }

        public override bool IsPlayerOnline(string playerRefId)
        {
            if (_plugin != null)
            {
                return _plugin.IsPlayerOnline(playerRefId);
            }

            return true;
        }

        public override object GetPlayerRef(string playerId)
        {
            return new Object(); // to bypass ref check in BaseTebexAdapter without rcon plugin
        }

        public override string ExpandUsernameVariables(string input, TebexApi.DuePlayer player)
        {
            string parsed = input;
            parsed = parsed.Replace("{id}", player.UUID);
            parsed = parsed.Replace("{username}", player.Name);
            return parsed;
        }

        public override string ExpandOfflineVariables(string input, TebexApi.PlayerInfo info)
        {
            string parsed = input;
            parsed = parsed.Replace("{id}", info.Id);
            parsed = parsed.Replace("{username}", info.Username);
            parsed = parsed.Replace("{name}", info.Username);

            if (parsed.Contains("{") || parsed.Contains("}"))
            {
                LogDebug($"Detected lingering curly braces after expanding offline variables!");
                LogDebug($"Input: {input}");
                LogDebug($"Parsed: {parsed}");
            }

            return parsed;
        }

        public override void MakeWebRequest(string url, string body, TebexApi.HttpVerb verb, TebexApi.ApiSuccessCallback onSuccess,
            TebexApi.ApiErrorCallback onApiError, TebexApi.ServerErrorCallback onServerError)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    LogDebug($" -> {verb.ToString()} {url} | {body}");
                    
                    // Set request headers
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"TebexRconAdapter/" + Version);
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
        
        #region Threading
        public static async void ExecuteEvery(TimeSpan interval, Action action)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!_isReady)
                {
                    // Due but not ready, try to execute every second
                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
                
                action();

                try
                {
                    // Due and ready, wait for the right interval
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
        
        #region Logging
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
            Console.WriteLine($"{logMessage}");
            _logger.WriteLine(logMessage);
            _logger.Flush();
        }
        
        public override void LogWarning(string message, string solution)
        {
            Log(message + " " + solution, LogLevel.Warning);
        }

        public override void LogWarning(string message, string solution, Dictionary<String, String> metadata)
        {
            Log(message, LogLevel.Warning);
            Log("- " + solution, LogLevel.Warning);

            if (PluginConfig.AutoReportingEnabled)
            {
                new PluginEvent(_plugin, _plugin.GetPlatform(), EnumEventLevel.WARNING, message).WithMetadata(metadata).Send(this);
            }
        }

        public override void LogError(string message)
        {
            Log(message, LogLevel.Error);
        }

        public override void LogError(string message, Dictionary<String, String> metadata)
        {
            Log(message, LogLevel.Error);
            if (PluginConfig.AutoReportingEnabled)
            {
                new PluginEvent(_plugin, _plugin.GetPlatform(), EnumEventLevel.ERROR, message).WithMetadata(metadata).Send(this);
            }
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
        
        #endregion
    }
}