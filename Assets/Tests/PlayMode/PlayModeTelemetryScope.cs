using System;
using NUnit.Framework;

namespace Hackathon.Tests
{
    // Interactive Test Runner sessions and real playtests share Unity's persistentDataPath.
    [SetUpFixture]
    public sealed class PlayModeTelemetryScope
    {
        private const string Flag = "NimbTelemetry.AutomatedRun";

        [OneTimeSetUp]
        public void RouteTestEncounters() => AppDomain.CurrentDomain.SetData(Flag, true);

        [OneTimeTearDown]
        public void RestorePlaytestDirectory() => AppDomain.CurrentDomain.SetData(Flag, null);
    }
}
