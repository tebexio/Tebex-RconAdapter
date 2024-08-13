
using System;

namespace Tebex.Triage
{
    public class TebexPlatform
    {
        private String _pluginVersion;
        private TebexTelemetry _telemetry;
        public TebexPlatform(String pluginVersion, TebexTelemetry _telemetry)
        {
            this._pluginVersion = pluginVersion;
            this._telemetry = _telemetry;

        }
    
        public TebexTelemetry GetTelemetry()
        {
            return _telemetry;
        }

        public string GetPluginVersion()
        {
            return _pluginVersion;
        }
    }    
}
