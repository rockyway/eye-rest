using System;
using EyeRest.Services;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Regression guard for the memory-pressure freeze root cause (see docs/plan/008,
    /// "Leak A"). <see cref="ConfigurationMigrator.MigrateFromJson"/> must reuse a single
    /// cached <see cref="System.Text.Json.JsonSerializerOptions"/>. A per-call
    /// <c>new JsonSerializerOptions</c> rebuilds the entire reflection-emit member-accessor
    /// cache (DynamicMethod / DynamicILGenerator / DynamicScope ...) on every load — and config
    /// is deserialized on every popup cycle — which accumulated on the heap and made the app a
    /// memory-pressure suspension target. A confirmed gcdump showed the System.Reflection.Emit
    /// cluster growing ~164 objects per cycle.
    /// </summary>
    public class ConfigurationMigratorLeakTests
    {
        // Representative v2 config — minimal but enough to exercise the AppConfiguration graph.
        private const string SampleConfigJson =
            "{\"eyeRest\":{\"intervalMinutes\":20,\"durationSeconds\":20,\"warningSeconds\":15}," +
            "\"break\":{\"intervalMinutes\":60,\"durationMinutes\":5,\"warningSeconds\":30}," +
            "\"audio\":{\"enabled\":true,\"volume\":50}," +
            "\"meta\":{\"schemaVersion\":2}}";

        [Fact]
        public void MigrateFromJson_RepeatedCalls_AllocateBoundedPerCall()
        {
            // Warm up: JIT the path and build the (now cached) reflection metadata once.
            for (int i = 0; i < 20; i++)
                _ = ConfigurationMigrator.MigrateFromJson(SampleConfigJson);

            const int iterations = 500;
            long before = GC.GetTotalAllocatedBytes(precise: true);
            for (int i = 0; i < iterations; i++)
                _ = ConfigurationMigrator.MigrateFromJson(SampleConfigJson);
            long after = GC.GetTotalAllocatedBytes(precise: true);

            long perCall = (after - before) / iterations;

            // With cached options the per-call cost is just JsonDocument.Parse + the small
            // AppConfiguration graph (a few KB). The pre-fix per-call `new JsonSerializerOptions`
            // rebuilt JsonTypeInfo + reflection-emit accessors for the whole type graph every
            // call — well over 100 KB/call. 50 KB cleanly separates the two regimes.
            Assert.True(perCall < 50_000,
                $"MigrateFromJson allocated ~{perCall:N0} bytes/call over {iterations} calls. " +
                "This suggests a per-call `new JsonSerializerOptions` (reflection-emit) regression — see docs/plan/008.");
        }

        [Fact]
        public void MigrateFromJson_RepeatedCalls_DoNotGrowRetainedHeap()
        {
            for (int i = 0; i < 50; i++)
                _ = ConfigurationMigrator.MigrateFromJson(SampleConfigJson);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < 2000; i++)
                _ = ConfigurationMigrator.MigrateFromJson(SampleConfigJson);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long after = GC.GetTotalMemory(forceFullCollection: true);

            long growthMb = (after - before) / (1024 * 1024);
            Assert.True(growthMb < 5,
                $"Retained heap grew {growthMb} MB over 2000 config migrations — JsonSerializerOptions leak regression (docs/plan/008).");
        }
    }
}
