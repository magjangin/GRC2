using System;
using System.IO;
using System.Reflection;

namespace GRC2.Tests
{
    internal static class TestAssemblyResolver
    {
        private static bool _initialized;
        private static readonly object _lock = new object();

        public static void Ensure()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                var gamePath = Environment.GetEnvironmentVariable("GRC2_GAME_PATH")
                    ?? @"H:\GUNVOLT RECORDS Cychronicle hwa";
                var dirs = new[]
                {
                    Path.Combine(gamePath, "GUNVOLT_RECORDS_Cychronicle_Data", "Managed"),
                    Path.Combine(gamePath, "MelonLoader", "net35"),
                    Path.Combine(gamePath, "MelonLoader", "net6"),
                };

                AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                {
                    var name = new AssemblyName(args.Name).Name + ".dll";
                    foreach (var dir in dirs)
                    {
                        var path = Path.Combine(dir, name);
                        if (File.Exists(path))
                            return Assembly.LoadFrom(path);
                    }
                    return null;
                };

                _initialized = true;
            }
        }
    }
}
