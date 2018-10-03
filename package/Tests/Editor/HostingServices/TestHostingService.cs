﻿using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    internal class TestHostingService : AbstractTestHostingService
    {
        public TestHostingService()
        {
            HostingServiceContentRoots = new List<string>();
            ProfileVariables = new Dictionary<string, string>();
        }

        public override void StartHostingService()
        {
            IsHostingServiceRunning = true;
        }

        public override void StopHostingService()
        {
            IsHostingServiceRunning = false;
        }
    }
}