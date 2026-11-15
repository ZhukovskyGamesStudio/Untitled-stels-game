using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The whole dev loop in ONE Unity launch (<c>Tools/dev.sh cycle</c>): build the missing assets, run the EditMode tests
    /// in-process, build the player. Starting the editor costs a minute or two each time; doing the three steps in three
    /// launches paid that three times. The player is a development build, and when nothing but code changed since the last
    /// one it is a scripts-only build (seconds instead of minutes): every content file's size and time go into a stamp next
    /// to the exe, and a different stamp means a full build. Env: FILTER (tests), FORCE / REBUILD / REBUILD_UI (assets),
    /// NOASSETS / NOTESTS / NOPLAYER = 1 to skip a step, FULL = 1 to force a full player build.
    /// </summary>
    public static class DevCycle
    {
        const string StampPath = "Builds/Windows/dev_build_stamp.txt";

        static bool Env(string name) => Environment.GetEnvironmentVariable(name) == "1";

        public static void RunBatch() => EditorApplication.Exit(Run());

        /// <summary>The same loop in an open editor (its player build blocks the editor until it is done).</summary>
        [MenuItem("Stealth/Dev Cycle (assets + tests + dev build)")]
        public static void RunInEditor()
        {
            int code = Run();
            Debug.Log(code == 0 ? "[Cycle] done" : "[Cycle] FAILED (see the lines above)");
        }

        /// <summary>The steps (see the class); 0 when every step that ran succeeded.</summary>
        static int Run()
        {
            int code = 0;
            try
            {
                if (!Env("NOASSETS"))
                {
                    AssetBuilder.BuildAll(Environment.GetCommandLineArgs().Contains("-force"));
                    AssetDatabase.SaveAssets();
                }
                if (!Env("NOTESTS") && !RunTests()) code = 1;
                if (code == 0 && !Env("NOPLAYER") && !BuildPlayer()) code = 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            return code;
        }

        // ------------------------------------------------------------------ tests

        sealed class Results : ICallbacks
        {
            public int Total, Passed, Failed, Skipped;
            public readonly List<string> Failures = new();
            public bool Done;

            public void RunStarted(ITestAdaptor tests) { }
            public void RunFinished(ITestResultAdaptor result) => Done = true;
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                Total++;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: Passed++; break;
                    case TestStatus.Failed:
                        Failed++;
                        Failures.Add($"FAILED {result.FullName} - {(result.Message ?? "").Trim().Replace('\n', ' ')}");
                        break;
                    default: Skipped++; break;
                }
            }
        }

        /// <summary>The EditMode tests, synchronously, in this process. True when none failed.</summary>
        static bool RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var results = new Results();
            api.RegisterCallbacks(results);
            var filter = new Filter { testMode = TestMode.EditMode };
            var only = Environment.GetEnvironmentVariable("FILTER");
            if (!string.IsNullOrEmpty(only)) filter.groupNames = new[] { only };
            api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
            api.UnregisterCallbacks(results);
            Debug.Log($"[Cycle] tests: total={results.Total} passed={results.Passed} failed={results.Failed}");
            foreach (var f in results.Failures) Debug.Log("[Cycle] " + f);
            return results.Failed == 0;
        }

        // ------------------------------------------------------------------ the player

        /// <summary>A development player: scripts only when the content is the same as at the last full one.</summary>
        static bool BuildPlayer()
        {
            string stamp = ContentStamp();
            bool scriptsOnly = !Env("FULL") && File.Exists(AssetBuilder.PlayerExe) && File.Exists(StampPath) && File.ReadAllText(StampPath) == stamp;
            var options = BuildOptions.Development | (scriptsOnly ? BuildOptions.BuildScriptsOnly : BuildOptions.None);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool ok = AssetBuilder.BuildPlayerWindows(options);
            Debug.Log($"[Cycle] player: {(scriptsOnly ? "scripts only" : "full")} development build, {(ok ? "ok" : "FAILED")} in {watch.Elapsed.TotalSeconds:0} s");
            if (ok && !scriptsOnly) File.WriteAllText(StampPath, stamp);
            if (!ok && File.Exists(StampPath)) File.Delete(StampPath);
            return ok;
        }

        /// <summary>
        /// What the player's data is made of: every file under Assets (but code) and the project settings, by path, size and
        /// time. The same stamp — only code changed: a scripts-only build is enough.
        /// </summary>
        static string ContentStamp()
        {
            var sb = new StringBuilder();
            foreach (var root in new[] { "Assets", "ProjectSettings" })
            {
                if (!Directory.Exists(root)) continue;
                var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var f in files)
                {
                    if (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs.meta", StringComparison.OrdinalIgnoreCase)) continue;
                    var info = new FileInfo(f);
                    sb.Append(f).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
                }
            }
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }
    }
}
