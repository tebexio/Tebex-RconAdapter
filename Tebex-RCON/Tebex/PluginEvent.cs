using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Triage
{
    public enum EnumEventLevel
    {
        INFO,
        WARNING,
        ERROR
    }

    public class PluginEvent
    {
        // Data attached to all plugin events, set via Init()
        public static string SERVER_IP = "";
        public static string SERVER_ID = "";
        public static string STORE_URL = "";
        public static bool IS_DISABLED = false;

        [JsonProperty("game_id")] private string GameId { get; set; }
        [JsonProperty("framework_id")] private string FrameworkId { get; set; }
        [JsonProperty("runtime_version")] private string RuntimeVersion { get; set; }

        [JsonProperty("framework_version")]
        private string FrameworkVersion { get; set; }

        [JsonProperty("plugin_version")] private string PluginVersion { get; set; }
        [JsonProperty("server_id")] private string ServerId { get; set; }
        [JsonProperty("event_message")] private string EventMessage { get; set; }
        [JsonProperty("event_level")] private String EventLevel { get; set; }
        [JsonProperty("metadata")] private Dictionary<string, string> Metadata { get; set; }
        [JsonProperty("trace")] private string Trace { get; set; }

        [JsonProperty("store_url")] private string StoreUrl { get; set; }
        
        [JsonProperty("server_ip")] private string ServerIp { get; set; }

        [JsonIgnore]
        public TebexPlatform platform;
        
        private RconPlugin _plugin;
        
        public PluginEvent(RconPlugin plugin, TebexPlatform platform, EnumEventLevel level, string message)
        {
            _plugin = plugin;
            platform = platform;

            TebexTelemetry tel = platform.GetTelemetry();

            GameId = "RCON-Adapter";
            FrameworkId = tel.GetServerSoftware();
            RuntimeVersion = tel.GetRuntimeVersion();
            FrameworkVersion = tel.GetServerVersion();
            PluginVersion = platform.GetPluginVersion();
            EventLevel = level.ToString();
            EventMessage = message;
            Trace = "";
            ServerIp = PluginEvent.SERVER_IP;
            ServerId = PluginEvent.SERVER_ID;
            StoreUrl = PluginEvent.STORE_URL;
        }

        public PluginEvent WithTrace(string trace)
        {
            Trace = trace;
            return this;
        }

        public PluginEvent WithMetadata(Dictionary<string, string> metadata)
        {
            Metadata = metadata;
            return this;
        }

        public void Send(BaseTebexAdapter adapter)
        {
            if (IS_DISABLED)
            {
                return;
            }

            List<PluginEvent> eventsList = new List<PluginEvent>(); //TODO
            eventsList.Add(this);
            adapter.MakeWebRequest("https://plugin-logs.tebex.io/events", JsonConvert.SerializeObject(eventsList), TebexApi.HttpVerb.POST,
                (code, body) =>
                {
                    if (code < 300 && code > 199) // success
                    {
                        adapter.LogDebug("Successfully sent plugin events");
                        return;
                    }
                    
                    adapter.LogDebug("Failed to send plugin logs. Unexpected response code: " + code);
                    adapter.LogDebug(body);
                }, (pluginLogsApiError) =>
                {
                    adapter.LogDebug("Failed to send plugin logs. Unexpected Tebex API error: " + pluginLogsApiError);
                }, (pluginLogsServerErrorCode, pluginLogsServerErrorResponse) =>
                {
                    adapter.LogDebug("Failed to send plugin logs. Unexpected server error: " + pluginLogsServerErrorResponse);
                });
        }
    }
}