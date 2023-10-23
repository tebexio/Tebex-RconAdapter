﻿using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON.Protocol;
using Tebex.Triage;

namespace Tebex.Adapters
{
    /** Provides logic that implements some RCON protocol */
    public class TebexRconAdapter : BaseTebexAdapter
    {
        public const string Version = "1.0.0-alpha.3";
        private const string ConfigFilePath = "tebex-config.json";

        private Type? _pluginType;
        private TebexRconPlugin? _plugin;
        protected static ProtocolManagerBase? Protocol;
        
        private TextWriter? _logger;
        private static bool _isReady = false;

        public override void Init()
        {
            // Setup log
            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var logName = $"TebexRcon-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.log";
            _logger = new StreamWriter(logName, true);
            LogInfo($"Log file is being saved to '{currentPath}\\{logName}'");
            
            LogInfo($"Tebex RCON Adapter Client {Version} | https://tebex.io/");
            
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
                    Console.WriteLine();
                    
                    LogInfo($"Loading game server plugin '{_pluginType}'...");
                    _plugin = Activator.CreateInstance(_pluginType, Protocol, this) as TebexRconPlugin;

                    // Connect to RCON after secret key is verified correct
                    Console.WriteLine();
                    LogInfo($"Connecting to game server at {PluginConfig.RconIp}:{PluginConfig.RconPort} using {Protocol.GetProtocolName()}...");
                    var successful = Protocol.Connect(PluginConfig.RconIp, PluginConfig.RconPort, PluginConfig.RconPassword, true);
                    if (!successful)
                    {
                        LogError($"> An error occurred while attempting connection.");
                        return;
                    }
                    
                    LogInfo(" > Successful RCON connection");
                    
                    LogInfo($"Configuring adapter for ${info.AccountInfo.GameType}");
                    
                    // Ensure the game at the RCON endpoint is the one for this account
                    if (!_plugin.AuthenticateGame(info.AccountInfo.GameType))
                    {
                        LogError($" > It does not appear that the server we connected to is a {info.AccountInfo.GameType} server");
                        LogError($" > Please check your secret key and store settings.");
                        Environment.Exit(1);
                        return;
                    }
                    LogInfo(" > Successfully authed the game server");
                    
                    Protocol.StartReconnectThread();
                    _isReady = true;
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

        public ProtocolManagerBase? GetProtocol()
        {
            return Protocol;
        }

        public void SetProtocol(ProtocolManagerBase protocol)
        {
            LogDebug($"Using {protocol.GetProtocolName()} protocol");
            Protocol = protocol;
        }

        public TebexRconPlugin GetPlugin()
        {
            return _plugin;
        }

        public void SetPluginType(Type type)
        {
            _pluginType = type;
        }
        
        public void InitPlugin(TebexRconPlugin plugin)
        {
            LogDebug($"Configuring {plugin.GetGameName()} plugin {plugin.GetPluginVersion()}");
            _plugin = plugin;
        }
        public override void SaveConfig()
        {
            string jsonText = JsonConvert.SerializeObject(PluginConfig, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, jsonText);
        }

        public override void ReplyPlayer(object player, string message)
        {
            throw new NotImplementedException();
        }

        public override void ExecuteOfflineCommand(TebexApi.Command command, string commandName, string[] args)
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
                        Protocol.Write(rconCommand);
                        var response = Protocol.Read(); //SendCommandAndReadResponse(2, cmd);
                    });
            }
            else // No delay, execute immediately
            {
                Protocol.Write(rconCommand);
                var response = Protocol.Read(); //SendCommandAndReadResponse(2, cmd);
            }
        }

        public override void ExecuteOnlineCommand(TebexApi.Command command, object playerObj, string commandName, string[] args)
        {
            LogInfo("Executing online command...");
            
            var cmd = ExpandUsernameVariables(command.CommandToRun, playerObj);
            cmd = _plugin.ExpandGameUsernameVariables(cmd, playerObj);
            
            LogInfo($"> Executing online command: {cmd}");
            Protocol.Write(cmd);
            var response = Protocol.Read(); //SendCommandAndReadResponse(2, cmd);
            LogInfo($"> Server responded: '{response}'");
        }

        public override bool IsPlayerOnline(string playerRefId)
        {
            return _plugin.IsPlayerOnline(playerRefId);
        }

        public override object GetPlayerRef(string playerId)
        {
            return _plugin.GetPlayerRef(playerId);
        }

        public override string ExpandUsernameVariables(string input, object playerObj)
        {
            // playerObj will be integer of player position/idx in /listplayers list.
            // This var is assigned prior by GetPlayerRef.
            input = input.Replace("{id}", playerObj.ToString());
            input = input.Replace("{username}", playerObj.ToString());
            return input;
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
            partialEvent.GameId = $"{_plugin.GetGameName()}";
            partialEvent.FrameworkId = "RCONAdapter";
            partialEvent.PluginVersion = _plugin.GetPluginVersion();
            partialEvent.ServerIp = Protocol.GetIpAndPort();
            
            return partialEvent;
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
            Console.WriteLine($"{message}");
            _logger.WriteLine(logMessage);
            _logger.Flush();
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
        
        #endregion
    }
}