using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Newtonsoft.Json;
using Tebex.Adapters;
using Tebex.API;
using Tebex.Plugins;
using Tebex.Triage;

namespace Tebex.RCON
{
    public class TebexRconAdapter : BaseTebexAdapter
    {
        private TextWriter Logger;
        public TebexRconClient Rcon;
        private const string ConfigFilePath = "tebex-config.json";
        private TebexRconPlugin Plugin;
        private static bool IsReady = false;
        public static readonly string Version = "1.0.0-alpha.3";
        
        public override void Init()
        {
            // Setup log
            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var logName = $"TebexRcon-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.log";
            Logger = new StreamWriter(logName, true);
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
                    
                    // Connect to RCON after secret key is verified correct
                    LogInfo($"Connecting to game server at {PluginConfig.RconIp}:{PluginConfig.RconPort}...");
                    Rcon = new TebexRconClient(PluginConfig.RconIp, PluginConfig.RconPort, PluginConfig.RconPassword, true);
                    Plugin = new ConanExilesPlugin(Rcon, this);

                    var (successful,error) = Rcon.Connect();
                    if (!successful)
                    {
                        LogError($"> An error occurred while attempting connection: {error.Message}");
                        return;
                    }
                    
                    LogInfo(" > Successful RCON connection");
                    
                    LogInfo($"Configuring adapter for ${info.AccountInfo.GameType}");
                    
                    // Ensure the game at the RCON endpoint is the one for this account
                    if (!Plugin.AuthenticateGame(info.AccountInfo.GameType))
                    {
                        LogError($" > It does not appear that the server we connected to is a {info.AccountInfo.GameType} server");
                        LogError($" > Please check your secret key and store settings.");
                        Environment.Exit(1);
                        return;
                    }
                    LogInfo(" > Successfully authed the game server");
                    
                    Rcon.StartReconnectThread();
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

        public string ObfuscateSensitiveString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            
            var sensitiveInfo = new List<String>();
            sensitiveInfo.Add(PluginConfig.SecretKey);
            sensitiveInfo.Add(PluginConfig.RconPassword);

            foreach (var element in sensitiveInfo)
            {
                if (string.IsNullOrEmpty(element))
                {
                    continue;
                }

                // Basic secret key isn't obfuscated
                if (element == "your-secret-key-here")
                {
                    continue;
                }
                
                if (input.Contains(element))
                {
                    input = input.Replace(element, $"[REDACTED|sz:{element.Length}]");
                }
            }

            return input;
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
                SetupRCONConnection();
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

        public void SetupRCONConnection()
        {
            while (true)
            {
                Console.Write("Enter server RCON IP (empty to skip): ");
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
                Console.Write("Enter server RCON port (empty to skip): ");
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
            
            Console.Write("Enter the RCON password (enter to skip): ");
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
        
        public void SaveConfig()
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
                        var response = Rcon.SendCommandAndReadResponse(2, rconCommand);
                    });
            }
            else // No delay, execute immediately
            {
                var response = Rcon.SendCommandAndReadResponse(2, rconCommand);
            }
        }

        public override void ExecuteOnlineCommand(TebexApi.Command command, object playerObj, string commandName, string[] args)
        {
            LogInfo("Executing online command...");
            
            var cmd = ExpandUsernameVariables(command.CommandToRun, playerObj);
            cmd = Plugin.ExpandGameUsernameVariables(cmd, playerObj);
            
            LogInfo($"> Executing online command: {cmd}");
            var response = Rcon.SendCommandAndReadResponse(2, cmd);
            LogInfo($"> Server responded: '{response}'");
        }

        public override bool IsPlayerOnline(string playerRefId)
        {
            return Plugin.IsPlayerOnline(playerRefId);
        }

        public override object GetPlayerRef(string playerId)
        {
            return Plugin.GetPlayerRef(playerId);
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
            partialEvent.GameId = $"{Plugin.GetGameName()}";
            partialEvent.FrameworkId = "RCONAdapter";
            partialEvent.PluginVersion = Plugin.GetPluginVersion();
            partialEvent.ServerIp = Rcon.GetIpAndPort();
            
            return partialEvent;
        }

        public void HandleTebexCommand(string command)
        {
            var splitCommand = command.Split(" ");
            var commandName = splitCommand[0];
            var commandArgs = splitCommand.Skip(1).ToArray();

            switch (commandName)
            {
                case "tebex.help":
                    TebexHelpCommand();
                    break;
                case "tebex.setup":
                    TebexSetupCommand();
                    break;
                case "tebex.debug":
                    TebexDebugCommand(commandArgs);
                    break;
                case "tebex.secret":
                    TebexSecretCommand(commandArgs);
                    break;
                case "tebex.refresh":
                    TebexRefreshCommand();
                    break;
                case "tebex.info":
                    TebexInfoCommand();
                    break;
                case "tebex.report":
                    TebexReportCommand(commandArgs);
                    break;
                case "tebex.forcecheck":
                    TebexForceCheckCommand();
                    break;
                case "tebex.lookup":
                    TebexLookupCommand(commandArgs);
                    break;
                case "tebex.packages":
                    TebexPackagesCommand();
                    break;
                default:
                    Console.WriteLine("Tebex command not recognized, use tebex.help for a list of commands.");
                    break;
            }
        }
        
        #region Commands

        public void TebexReportCommand(string[] args)
        {
            if (args.Length == 0) // require /confirm to send
            {
                LogInfo("Please run `tebex.report confirm 'Your description here'` to submit your report. The following information will be sent to Tebex: ");
                LogInfo("- Your game version, store id, and server IP.");
                LogInfo("- Your username and IP address.");
                LogInfo("- Please include a short description of the issue you were facing.");
            }

            if (args.Length >= 2 && args[0] == "confirm")
            {
                LogInfo("Sending your report to Tebex...");
                
                var triageEvent = new TebexTriage.ReportedTriageEvent();
                triageEvent.GameId = $"{Plugin.GetGameName()}";
                triageEvent.FrameworkId = "RCONAdapter";
                triageEvent.PluginVersion = Plugin.GetPluginVersion();
                triageEvent.ServerIp = Rcon.GetIpAndPort();
                triageEvent.ErrorMessage = "Player Report: " + string.Join(" ", args[1..]);
                triageEvent.Trace = "";
                triageEvent.Metadata = new Dictionary<string, string>()
                {
                
                };
                triageEvent.UserIp = "";
                
                ReportManualTriageEvent(triageEvent, (code, body) =>
                {
                    LogInfo("Your report has been sent. Thank you!");
                }, (code, body) =>
                {
                    LogInfo("An error occurred while submitting your report. Please contact our support team directly.");
                    LogInfo("Error: " + body);
                });
                
                return;
            }
            
            LogInfo("Usage: tebex.report <confirm> '<message>'");
        }

        public void TebexLookupCommand(string[] args)
        {
            if (args.Length != 1)
            {
                LogInfo($"Usage: tebex.lookup <playerId/playerUsername>");
                return;
            }

            GetUser(args[0], (code, body) =>
            {
                var response = JsonConvert.DeserializeObject<TebexApi.UserInfoResponse>(body);
                LogInfo($"Username: {response.Player.Username}");
                LogInfo($"Id: {response.Player.Id}");
                LogInfo($"Payments Total: ${response.Payments.Sum(payment => payment.Price)}");
                LogInfo($"Chargeback Rate: {response.ChargebackRate}%");
                LogInfo($"Bans Total: {response.BanCount}");
                LogInfo($"Payments: {response.Payments.Count}");
            }, error =>
            {
                LogInfo(error.ErrorMessage);
            });
        }

        public void TebexPackagesCommand()
        {
            GetPackages(packages =>
            {
                PrintPackages(packages);
            });
        }
        
        private void PrintPackages(List<TebexApi.Package> packages)
        {
            // Index counter for selecting displayed items
            var packIndex = 1;

            LogInfo("---------------------------------");
            LogInfo("      PACKAGES AVAILABLE         ");
            LogInfo("---------------------------------");

            // Sort categories in order and display
            var orderedPackages = packages.OrderBy(package => package.Order).ToList();
            for (var i = 0; i < packages.Count; i++)
            {
                var package = orderedPackages[i];
                // Add additional flair on sales
                LogInfo($"[P{packIndex}] {package.Name}");
                LogInfo($"Category: {package.Category.Name}");
                LogInfo($"Description: {package.Description}");

                if (package.Sale != null && package.Sale.Active)
                {
                    LogInfo($"Original Price: {package.Price} {package.GetFriendlyPayFrequency()}  SALE: {package.Sale.Discount} OFF!");
                }
                else
                {
                    LogInfo($"Price: {package.Price} {package.GetFriendlyPayFrequency()}");
                }

                //LogInfo($"Purchase with 'tebex.checkout P{packIndex}' or 'tebex.checkout {package.Id}'");
                LogInfo("--------------------------------");

                packIndex++;
            }
        }
        public void TebexRefreshCommand()
        {
            LogInfo("Refreshing listings...");
            Cache.Instance.Remove("packages");
            Cache.Instance.Remove("categories");
            
            RefreshListings((code, body) =>
            {
                if (Cache.Instance.HasValid("packages") && Cache.Instance.HasValid("categories"))
                {
                    var packs = (List<TebexApi.Package>)Cache.Instance.Get("packages").Value;
                    var categories = (List<TebexApi.Category>)Cache.Instance.Get("categories").Value;
                    LogInfo($"Fetched {packs.Count} packages out of {categories.Count} categories");
                }
            });
        }
        public void TebexHelpCommand()
        {
            LogInfo("Tebex Commands Available:");
            LogInfo("-- Administrator Commands --");
            LogInfo("tebex.setup                       - Starts the guided setup.");
            LogInfo("tebex.debug <on/off>              - Enables or disables debug logging.");
            LogInfo("tebex.secret <secretKey>          - Sets your server's secret key.");
            //LogInfo("tebex.sendlink <player> <packId>  - Sends a purchase link to the provided player.");
            LogInfo("tebex.forcecheck                  - Forces the command queue to check for any pending purchases.");
            LogInfo("tebex.refresh                     - Refreshes store information, packages, categories, etc.");
            LogInfo("tebex.report                      - Generates a report for the Tebex support team.");
            //LogInfo("tebex.ban <playerId>              - Bans a player from using your Tebex store.");
            LogInfo("tebex.lookup <playerId>           - Looks up store statistics for the given player.");
            
            //LogInfo("-- User Commands --");
            //LogInfo("tebex.info                       - Get information about this server's store.");
            //LogInfo("tebex.categories                 - Shows all item categories available on the store.");
            LogInfo("tebex.packages <opt:categoryId>  - Shows all item packages available in the store or provided category.");
            //LogInfo("tebex.checkout <packId>          - Creates a checkout link for an item. Visit to purchase.");
            //LogInfo("tebex.stats                      - Gets your stats from the store, purchases, subscriptions, etc.");
        }

        public void TebexSetupCommand()
        {
            DoSetup();
        }
        
        public void TebexDebugCommand(string[] args)
        {
            if (args.Length != 1)
            {
                LogInfo("Invalid syntax. Usage: \"tebex.debug <on/off>\"");
                return;
            }

            if (IsTruthy(args[0]))
            {
                PluginConfig.DebugMode = true;
                SaveConfig();
            } 
            else if (IsFalsy(args[0]))
            {
                PluginConfig.DebugMode = false;
                SaveConfig();    
            }
            else
            {
                LogInfo("Invalid syntax. Usage: \"tebex.debug <on/off>\"");
            }
            
            LogInfo($"Debug mode: {PluginConfig.DebugMode}");
        }
        
        public void TebexSecretCommand(string[] args)
        {
            if (args.Length != 1)
            {
                LogInfo("Invalid syntax. Usage: \"tebex.secret <secret>\"");
                return;
            }

            var oldKey = PluginConfig.SecretKey;
            LogInfo("Setting your secret key...");
            PluginConfig.SecretKey = args[0];

            // Reset store info so that we don't fetch from the cache
            Cache.Instance.Remove("information");

            // Any failure to set secret key is logged to console automatically
            FetchStoreInfo(info =>
            {
                LogInfo($"This server is now registered as server {info.ServerInfo.Name} for the web store {info.AccountInfo.Name}");
            }, tebexError =>
            {
                LogError($"Tebex error while setting your secret key: {tebexError.ErrorMessage}");
                PluginConfig.SecretKey = oldKey;
            }, (code, body) =>
            {
                LogError($"Error while setting your secret key: {body}");
                PluginConfig.SecretKey = oldKey;
            });
            
            SaveConfig();
        }

        public void TebexInfoCommand()
        {
            FetchStoreInfo((info) =>
            {
                LogInfo("Information for this server:");
                LogInfo($" > {info.ServerInfo.Name} for webstore {info.AccountInfo.Name}");
                LogInfo($" > Server prices are in {info.AccountInfo.Currency.Iso4217}");
                LogInfo($" > Webstore domain {info.AccountInfo.Domain}");
            });
        }

        public void TebexForceCheckCommand()
        {
            LogInfo("Forcing check of all Tebex operations...");
            ProcessCommandQueue(true);
            ProcessJoinQueue(true);
            DeleteExecutedCommands(true);
            RefreshStoreInformation(true);
            LogInfo("> Force check completed.");
        }
        
        #endregion
        
        #region Threading
        public static async void ExecuteEvery(TimeSpan interval, Action action)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!IsReady)
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
        
        #endregion
        
        private static readonly HashSet<string> TruthyStrings = new HashSet<string>
        {
            "true", "yes", "on", "1", "enabled", "enable"
        };

        private static readonly HashSet<string> FalsyStrings = new HashSet<string>
        {
            "false", "no", "off", "0", "disabled", "disable"
        };

        public static bool IsTruthy(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            return TruthyStrings.Contains(input.Trim().ToLowerInvariant());
        }

        public static bool IsFalsy(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            return FalsyStrings.Contains(input.Trim().ToLowerInvariant());
        }
    }
}