﻿using System;

namespace Tebex.Triage
{
    public class TebexTelemetry
    {
        private string _serverSoftware;
        private string _serverVersion;
        private string _runtimeVersion;

        public TebexTelemetry(String serverSoftware, String serverVersion, String runtimeVersion)
        {
            _serverSoftware = serverSoftware;
            _serverVersion = serverVersion;
            _runtimeVersion = runtimeVersion;
        }
    
        public string GetServerSoftware()
        {
            return _serverSoftware;
        }

        public string GetRuntimeVersion()
        {
            return _runtimeVersion;
        }

        public string GetServerVersion()
        {
            return _serverVersion;
        }
    }   
}