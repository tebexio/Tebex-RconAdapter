using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON.Protocol;
using Tebex.Triage;
using Tebex.Util;

namespace Tebex.Adapters
{
    /// <summary>
    /// TebexRconAdapter implements Tebex plugin functions via an RCON connection.
    /// </summary>
    public class TebexRconAdapter : BaseTebexAdapter
    {
        /// <summary>
        /// Map of Tebex game IDs to the appropriate RCON plugin. An RCON plugin is loaded at startup to provide the
        /// necessary enhanced RCON integration functions.
        /// </summary>
        private Dictionary<string, Type> PLUGINS = new Dictionary<string, Type>()
        {
            {"Minecraft: Java Edition", typeof(MinecraftPlugin)},
            {"ARK: Survival Evolved", typeof(ArkPlugin)},
            {"Rust", typeof(RustPlugin)},
            {"7 Days To Die", typeof(SevenDaysPlugin)},
            {"Conan Exiles", typeof(ConanExilesPlugin)},
        };
        
        public const string Version = "1.1.1";
        private const string ConfigFilePath = "./tebex-config.json";
        
        private RconPlugin? _plugin;
        private RconConnection _rcon;
        private TextWriter _logger;
        private static bool _isReady = false;

        private TebexConfig? _startupConfig;
        
        /// <summary>
        /// Gets the RconConnection instance through which commands can be sent.  This is available after Init().
        /// The underlying connection may not always be active, but should trigger reconnection if an established connection
        /// is lost.
        /// </summary>
        /// <returns></returns>
        public RconConnection GetRcon()
        {
            return _rcon;
        }
        
        public override void Init()
        {
            Instance = this;
            
            // Setup log paths
            var currentPath = AppContext.BaseDirectory;
            char dirSeparator = Path.DirectorySeparatorChar;
            
            var logName = $"TebexRcon-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.log";
            var logPath = currentPath + dirSeparator + logName;
            _logger = new StreamWriter(logPath, true);
            LogInfo(Ansi.White($"Log file is being saved to '{currentPath}{dirSeparator}{logName}'"));
            LogInfo($"{Ansi.Yellow("Tebex RCON Adapter Client " + Version)} | {Ansi.Blue(Ansi.Underline("https://tebex.io"))}");
            
            //startupConfig is expected to be set from SetStartupArguments before Init()
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
                LogInfo(Warn("Your webstore's secret key has not been configured yet. Initiating setup..."));
                DoSetup();
            }

            FetchStoreInfo(info =>
            {
                // Set plugin event vars for error reporting
                PluginEvent.SERVER_IP = PluginConfig.RconIp;
                PluginEvent.STORE_URL = info.AccountInfo.Domain;
                PluginEvent.SERVER_ID = info.ServerInfo.Id.ToString();
                
                LogInfo(Success($"Validated Tebex store {Ansi.Blue(info.AccountInfo.Name)} running game type {Ansi.Blue(info.AccountInfo.GameType)}"));
                
                String gameType = info.AccountInfo.GameType;

                // Attempt to create plugin instance for the store's game type
                try
                {
                    _plugin = Activator.CreateInstance(PLUGINS[gameType], this) as RconPlugin;
                }
                catch (Exception e)
                {
                    LogInfo(Error(e.Message));
                }

                // If we can't create a plugin, enhanced features won't be available
                if (_plugin == null)
                {
                    LogWarning(Warn($"We do not support enhanced RCON for '{gameType}'."),
                        "Commands will be sent, but game-specific features such as players online will be disabled.");
                }
                else
                {
                    LogInfo(Success("Loaded Tebex RCON plugin for " + gameType + ". Enhanced RCON support is enabled."));
                }
                
                try
                {
                    /*
                     * When a plugin is available, it may override CreateRconConnection to implement a different RCON protocol (BattleNet, Websocket, etc.)
                     * Otherwise we assume a basic RCON connection implementing the Minecraft protocol. The connection is not actually made until Connect() is called.
                     */
                    if (_plugin != null)
                    {
                        _rcon = _plugin.CreateRconConnection(PluginConfig.RconIp, PluginConfig.RconPort, PluginConfig.RconPassword);                        
                    }
                    else
                    {
                        _rcon = new RconConnection(this, PluginConfig.RconIp, PluginConfig.RconPort,
                            PluginConfig.RconPassword);
                    }
                    
                    LogInfo(Ansi.Yellow($"Connecting to RCON server at {PluginConfig.RconIp}:{PluginConfig.RconPort} using {_rcon.GetType().Name}..."));
                    Tuple<bool, string> connectResult = _rcon.Connect(); // returns success or false and an error message
                    
                    if (!connectResult.Item1) // failed to connect
                    {
                        LogError(Error($"{connectResult.Item2}. Check that your RCON connection parameters are correct, and try again."));

                        Console.Write(Warn("Would you like to re-enter your RCON server information? [Y/N]: "));
                        var doInitiateSetup = Console.ReadLine();
                        if (string.IsNullOrEmpty(doInitiateSetup) || doInitiateSetup.ToLower().Equals("n"))
                        {
                            Environment.Exit(1);
                            return;
                            
                        }
                        
                        // User indicates they want to re-run setup. Start with RCON IP which will transition to prompt for port and password as well.
                        GetUserRconIp();
                        
                        Console.WriteLine(Success("Configuration updated. Retrying..."));
                        Init();
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
                LogInfo("Use `tebex.secret` to set your secret key and restart the application.");
            });
        }

        /// <summary>
        /// Initializes a new configuration with the provided startup parameters. This should be run before Init().
        /// </summary>
        /// <param name="key">The store secret key.</param>
        /// <param name="host">The RCON server host/ip.</param>
        /// <param name="port">The RCON port.</param>
        /// <param name="auth">The RCON login password.</param>
        /// <param name="debug">Whether we are in debug mode</param>
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
                _startupConfig.DisableOnlineCheck = fileConfig.DisableOnlineCheck;
                
                // If debug mode wasn't requested from env or command line, ensure we read
                // the set value from the config file
                if (!newStartupConfig.DebugMode)
                {
                    _startupConfig.DebugMode = fileConfig.DebugMode;    
                }
            }
        }
        
        /// <summary>
        /// Reads or creates the configuration file for RCON Adapter
        /// </summary>
        /// <returns><see cref="BaseTebexAdapter.TebexConfig"/> with loaded values from config file. Default values if no config file found.</returns>
        private TebexConfig ReadConfig()
        {
            var cfg = new TebexConfig();
            if (File.Exists(ConfigFilePath))
            {
                string jsonText = File.ReadAllText(ConfigFilePath);
                cfg = JsonConvert.DeserializeObject<TebexConfig>(jsonText);
            }
            else
            {
                cfg = new TebexConfig(); // default settings are applied
                SaveConfig(cfg); // saves immediately to disk
            }

            return cfg;
        }

        public override void SaveConfig(TebexConfig config)
        {
            string jsonText = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, jsonText);
        }

        public override void ExecuteOfflineCommand(TebexApi.Command command, string commandName, string[] args)
        {
            var rconCommand = ExpandOfflineVariables(command.CommandToRun, command.Player);
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
            else // no error, successful response
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

        public override bool IsPlayerOnline(TebexApi.DuePlayer duePlayer)
        {
            if (PluginConfig.DisableOnlineCheck)
            {
                return true;
            }
                
            // Passthrough to an enabled plugin to determine if players are online.
            if (_plugin != null)
            {
                return _plugin.IsPlayerOnline(duePlayer);
            }

            // Without a plugin we assume the player is online to always attempt online command requests
            return true;
        }

        public override object GetPlayerRef(string playerId)
        {
            if (_plugin != null && _plugin.HasCustomPlayerRef())
            {
                return _plugin.GetPlayerRef(playerId);
            }
            else
            {
                // To bypass ref check in BaseTebexAdapter without an RCON plugin defined   
                return new Object(); 
            }
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
            parsed = parsed.Replace("{id}", info.Uuid);
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
                    // Log details of the send
                    LogDebug($" -> {verb.ToString()} {url} | {body}");

                    // Set required request headers
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"TebexRconAdapter/" + Version);
                    httpClient.DefaultRequestHeaders.Add("X-Tebex-Secret", PluginConfig.SecretKey);
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    
                    StringContent data = new StringContent(body, Encoding.UTF8, "application/json");

                    // Determine how to send our request and wait for it to complete.
                    Task<HttpResponseMessage> task;
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
                        default:
                            LogError("Unsupported HTTP method: " + verb);
                            return;
                    }

                    task.Wait();

                    HttpResponseMessage response = task.Result;
                    var code = (int)response.StatusCode;
                    LogDebug($"{code} <- {verb.ToString()} {url}"); // Log the response value for debug

                    // Read the actual response body/content, if present
                    var readTask = response.Content.ReadAsStringAsync();
                    readTask.Wait();
                    var content = readTask.Result;
                    LogDebug($" | {content}");

                    // Pass the response body to any provided handler functions based on the type of response
                    if (response.IsSuccessStatusCode)
                    {
                        onSuccess?.Invoke(code, content);
                    }
                    else if (code >= 400 && code <= 499)
                    {
                        // We expect standard formatted TebexErrors for HTTP client error responses
                        var tebexError = JsonConvert.DeserializeObject<TebexApi.TebexError>(content);
                        onApiError?.Invoke(tebexError);
                    }
                    else if (code >= 500)
                    {
                        // Server error responses include the error code and any content read from the server.
                        // The response may not necessarily be a JSON response, use caution if attempting to parse as such.
                        onServerError?.Invoke(code, content);
                    }
                }
            }
            catch (Exception ex)
            {
                // Unexpected exceptions still write to server error with the exception message.
                onServerError?.Invoke(0, ex.Message);
            }
        }
        
        public override bool IsTebexReady()
        {
            return _isReady;
        }
        
        #region Threading
        
        /// <summary>
        /// Executes a given Action across the given TimeSpan. This creates an async Task and checks each second if we are
        /// at the designated time interval before ultimately running the action when it's time.
        /// </summary>
        /// <param name="interval">The interval the Action should be performed at.</param>
        /// <param name="action">The action/function to perform.</param>
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

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Instance.LogError("Recurring action failed: " + e.Message);
                }

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

        /// <summary>
        /// Executes a given action once after a set number of seconds. Used to delay command execution per each command.
        /// Command execution delays can be set on a per-command basis.
        /// </summary>
        /// <param name="delaySeconds">The number of seconds to delay execution.</param>
        /// <param name="action">The action/function to perform.</param>
        public static async void ExecuteOnce(int delaySeconds, Action action)
        {
            await Task.Delay(delaySeconds);
            action();
        }
        
        #endregion
        
        #region Logging
        // Basic implementation of a file logger
        
        private enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }
        
        /// <summary>
        /// TebexRconAdapter._log logs a timestamped message to both the console and the log file. 
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log level to use.</param>
        private void _log(string message, LogLevel level)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] [{level}] {message}";
            Console.WriteLine($"{logMessage}");
            _logger.WriteLine(logMessage);
            _logger.Flush();
        }
        
        public override void LogWarning(string message, string solution)
        {
            _log(message + " " + solution, LogLevel.Warning);
        }

        public override void LogWarning(string message, string solution, Dictionary<String, String> metadata)
        {
            _log(message, LogLevel.Warning);
            _log("- " + solution, LogLevel.Warning);

            if (PluginConfig.AutoReportingEnabled)
            {
                new PluginEvent(_plugin, _plugin.GetPlatform(), EnumEventLevel.WARNING, message).WithMetadata(metadata).Send(this);
            }
        }

        public override void LogError(string message)
        {
            _log(message, LogLevel.Error);
            
            if (PluginConfig.AutoReportingEnabled)
            {
                new PluginEvent(_plugin, _plugin.GetPlatform(), EnumEventLevel.ERROR, message).Send(this);
            }
        }

        public override void LogError(string message, Dictionary<String, String> metadata)
        {
            _log(message, LogLevel.Error);
            if (PluginConfig.AutoReportingEnabled)
            {
                new PluginEvent(_plugin, _plugin.GetPlatform(), EnumEventLevel.ERROR, message).WithMetadata(metadata).Send(this);
            }
        }
        
        public override void LogInfo(string message)
        {
            _log(message, LogLevel.Info);
        }

        public override void LogDebug(string message)
        {
            if (PluginConfig.DebugMode)
            {
                _log(message, LogLevel.Debug);    
            }
        }
        
        #endregion

        /// <summary>
        /// OnProcessExit is called when the app is closing, either via signal or Environment.Exit(). We clear our pending
        /// plugin logs when the process exits.
        /// </summary>
        public static void OnProcessExit(object sender, EventArgs e)
        {
            PluginEvent.SendAllEvents(Instance);
        }
    }
}