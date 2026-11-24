using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ValorantApp;
using ValorantApp.Database.Extensions;

namespace ValorantAppTests
{
    [TestClass]
    [DoNotParallelize]
    public class OutOfProcessE2ETests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;

        public static bool LastServerUsed127 { get; private set; }

        #region Helpers

        private static (string Json, string MatchId, long Epoch) LoadRewrittenFixture(DateTime matchTime)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_10_31_2023.json");
            var root = JObject.Parse(File.ReadAllText(path));
            var meta = root["data"]![0]!["metadata"]!;
            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            meta["game_start"] = epoch;
            meta["game_start_patched"] = matchTime.ToString("dddd, MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
            return (root.ToString(Formatting.None), meta["matchid"]!.ToString(), epoch);
        }

        private static void SeedDatabase(string sqlitePath, string puuid, ulong discordId, ulong channelId)
        {
            using var conn = new SqliteConnection($"Data Source={sqlitePath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ValorantUsers (val_username, val_tagname, val_affinity, val_puuid, disc_id) VALUES (@u, @t, @a, @p, @d);
                INSERT INTO ValorantChannelMapping (val_puuid, disc_channel_id) VALUES (@p, @c);";
            cmd.Parameters.AddWithValue("@u", "Ehtan");
            cmd.Parameters.AddWithValue("@t", "NA1");
            cmd.Parameters.AddWithValue("@a", "na");
            cmd.Parameters.AddWithValue("@p", puuid);
            cmd.Parameters.AddWithValue("@d", (long)discordId);
            cmd.Parameters.AddWithValue("@c", (long)channelId);
            cmd.ExecuteNonQuery();
            SqliteConnection.ClearAllPools();
        }

        private static List<JObject> ReadEmbedLines(string recordPath)
        {
            var list = new List<JObject>();
            if (!File.Exists(recordPath)) return list;
            try
            {
                using var fs = new FileStream(recordPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var obj = JObject.Parse(line);
                        if (obj["type"]?.ToString() == "embed") list.Add(obj);
                    }
                    catch (JsonException) { }
                }
            }
            catch (IOException) { }
            return list;
        }

        private static async Task<List<JObject>> WaitForEmbedsAsync(string recordPath, int minCount = 1, TimeSpan? timeout = null)
        {
            var sw = Stopwatch.StartNew();
            var max = timeout ?? TimeSpan.FromSeconds(30);
            while (sw.Elapsed < max)
            {
                var embeds = ReadEmbedLines(recordPath);
                if (embeds.Count >= minCount) return embeds;
                await Task.Delay(100);
            }
            throw new TimeoutException($"Timed out after {max.TotalSeconds}s waiting for {minCount} embed(s) in {recordPath}");
        }

        private sealed class FakeHenrikServer : IAsyncDisposable
        {
            private readonly HttpListener _listener;
            private readonly CancellationTokenSource _cts = new();
            private readonly Task _loopTask;
            public string BaseUrl { get; }
            public ConcurrentQueue<string> RequestedPaths { get; } = new();

            private FakeHenrikServer(HttpListener listener, string baseUrl, string fixtureJson)
            {
                _listener = listener;
                BaseUrl = baseUrl;
                byte[] matchBytes = Encoding.UTF8.GetBytes(fixtureJson);
                byte[] notFoundBytes = Encoding.UTF8.GetBytes("{\"status\":404,\"message\":\"Not Found\"}");

                _loopTask = Task.Run(async () =>
                {
                    while (!_cts.Token.IsCancellationRequested && _listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try { ctx = await _listener.GetContextAsync(); } catch { break; }
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string raw = ctx.Request.RawUrl ?? string.Empty;
                                RequestedPaths.Enqueue(raw);
                                bool match = raw.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase);
                                byte[] b = match ? matchBytes : notFoundBytes;
                                ctx.Response.StatusCode = match ? 200 : 404;
                                ctx.Response.ContentType = "application/json";
                                ctx.Response.ContentLength64 = b.Length;
                                await ctx.Response.OutputStream.WriteAsync(b, 0, b.Length);
                            }
                            catch { }
                            finally { try { ctx.Response.Close(); } catch { } }
                        });
                    }
                });
            }

            public static FakeHenrikServer Start(string fixtureJson)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    int port;
                    using (var tcp = new TcpListener(IPAddress.Loopback, 0))
                    {
                        tcp.Start();
                        port = ((IPEndPoint)tcp.LocalEndpoint).Port;
                        tcp.Stop();
                    }

                    var listener = new HttpListener();
                    string prefix127 = $"http://127.0.0.1:{port}/";
                    try
                    {
                        listener.Prefixes.Add(prefix127);
                        listener.Start();
                        LastServerUsed127 = true;
                        return new FakeHenrikServer(listener, prefix127, fixtureJson);
                    }
                    catch (HttpListenerException ex) when (ex.ErrorCode != 5) { listener.Close(); continue; }
                    catch (HttpListenerException)
                    {
                        listener.Close();
                        listener = new HttpListener();
                        string prefixLocalhost = $"http://localhost:{port}/";
                        try
                        {
                            listener.Prefixes.Add(prefixLocalhost);
                            listener.Start();
                            LastServerUsed127 = false;
                            return new FakeHenrikServer(listener, prefixLocalhost, fixtureJson);
                        }
                        catch { listener.Close(); continue; }
                    }
                }
                throw new InvalidOperationException("Could not bind HttpListener to loopback port.");
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                try { _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }
                try { await _loopTask; } catch { }
                _cts.Dispose();
            }
        }

        private sealed class AppProcess : IAsyncDisposable
        {
            public string TempDir { get; }
            public string SqlitePath { get; }
            public string RecordPath { get; }
            public Process Process { get; }
            public StringBuilder Stdout { get; } = new();
            public StringBuilder Stderr { get; } = new();
            private readonly TaskCompletionSource<bool> _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            private AppProcess(string tempDir, string sqlitePath, string recordPath, ProcessStartInfo psi)
            {
                TempDir = tempDir;
                SqlitePath = sqlitePath;
                RecordPath = recordPath;
                Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                Process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (Stdout) Stdout.AppendLine(e.Data);
                    if (e.Data.Trim() == "READY") _readyTcs.TrySetResult(true);
                };
                Process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (Stderr) Stderr.AppendLine(e.Data);
                };
                Process.Exited += (_, _) => _readyTcs.TrySetResult(false);
                Process.Start();
                Process.BeginOutputReadLine();
                Process.BeginErrorReadLine();
            }

            public static AppProcess Start(string henrikUrl, bool seedDatabase = true, int pollInitialDelayMs = 0, int pollIntervalMs = 300)
            {
                string tempDir = Path.Combine(Path.GetTempPath(), $"valorantapp-oop-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                string sqlitePath = Path.Combine(tempDir, "e2e.db");
                string recordPath = Path.Combine(tempDir, "rec.jsonl");

                if (seedDatabase)
                {
                    ITable.CreateTables($"Data Source={sqlitePath}");
                    SeedDatabase(sqlitePath, EthanPuuid, EthanDiscordId, TestChannelId);
                }

                string appDll = typeof(global::ValorantApp.ValorantApp).Assembly.Location;
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{appDll}\"",
                    WorkingDirectory = tempDir,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.EnvironmentVariables[AppOptions.TestModeVar] = "1";
                psi.EnvironmentVariables[AppOptions.HenrikUrlVar] = henrikUrl;
                psi.EnvironmentVariables[AppOptions.SqlitePathVar] = sqlitePath;
                psi.EnvironmentVariables[AppOptions.RecordPathVar] = recordPath;
                psi.EnvironmentVariables[AppOptions.PollInitialDelayMsVar] = pollInitialDelayMs.ToString();
                psi.EnvironmentVariables[AppOptions.PollIntervalMsVar] = pollIntervalMs.ToString();

                return new AppProcess(tempDir, sqlitePath, recordPath, psi);
            }

            public async Task WaitForReadyAsync(TimeSpan? timeout = null)
            {
                var limit = timeout ?? TimeSpan.FromSeconds(60);
                var completed = await Task.WhenAny(_readyTcs.Task, Task.Delay(limit));
                if (completed != _readyTcs.Task || !_readyTcs.Task.Result)
                {
                    Assert.Fail($"Process failed to reach READY. Exited: {Process.HasExited}, ExitCode: {(Process.HasExited ? Process.ExitCode.ToString() : "N/A")}.\nStdout:\n{GetStdout()}\nStderr:\n{GetStderr()}");
                }
            }

            public string GetStdout() { lock (Stdout) return Stdout.ToString(); }
            public string GetStderr() { lock (Stderr) return Stderr.ToString(); }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    if (!Process.HasExited)
                    {
                        try { Process.StandardInput.Close(); } catch { }
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        try { await Process.WaitForExitAsync(cts.Token); }
                        catch (OperationCanceledException)
                        {
                            try { Process.Kill(entireProcessTree: true); Process.WaitForExit(5000); } catch { }
                        }
                    }
                }
                catch { }
                finally
                {
                    try { Process.Dispose(); } catch { }
                    SqliteConnection.ClearAllPools();
                    for (int i = 0; i < 5; i++)
                    {
                        try { if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true); break; }
                        catch (Exception) when (i < 4) { Thread.Sleep(50); }
                        catch { break; }
                    }
                }
            }
        }

        #endregion

        #region Tests

        [TestMethod]
        public async Task OutOfProcess_NewMatch_PostsRecapToRecorderAndStoresRows()
        {
            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, epoch) = LoadRewrittenFixture(matchTime);

            await using var server = FakeHenrikServer.Start(fixtureJson);
            await using var app = AppProcess.Start(server.BaseUrl);

            await app.WaitForReadyAsync(TimeSpan.FromSeconds(60));
            var embeds = await WaitForEmbedsAsync(app.RecordPath, 1, TimeSpan.FromSeconds(30));

            var embed = embeds[0];
            Assert.AreEqual(TestChannelId, embed["channelId"]!.Value<ulong>());
            Assert.AreEqual($"<@{EthanDiscordId}>", embed["text"]!.Value<string>());
            Assert.AreEqual("SwiftPlay - Ascent", embed["title"]!.Value<string>());
            Assert.AreEqual($"https://tracker.gg/valorant/match/{matchId}", embed["url"]!.Value<string>());

            var fields = (JArray)embed["fields"]!;
            Assert.AreEqual(2, fields.Count);
            Assert.AreEqual("<:reyna:1251243990582300712> Ehtan | <:unr:1214734089165475850>", fields[0]["name"]!.Value<string>());
            Assert.AreEqual("Combat Score: 227, K/D/A: 6/6/1\nHeadshot: 63.64%, RR: 0", fields[0]["value"]!.Value<string>());
            Assert.AreEqual($"__{new string(' ', 40)}__\n\nMatch Stats", fields[1]["name"]!.Value<string>());
            Assert.AreEqual($"<t:{epoch}:f>, 11 minutes\nRounds 3 : 5\nAverage Ranks <:unr:1214734089165475850> : <:unr:1214734089165475850>", fields[1]["value"]!.Value<string>());

            using (var conn = new SqliteConnection($"Data Source={app.SqlitePath};Pooling=False"))
            {
                conn.Open();
                using var cmd1 = conn.CreateCommand();
                cmd1.CommandText = "SELECT COUNT(*) FROM Matches WHERE match_id = @id";
                cmd1.Parameters.AddWithValue("@id", matchId);
                Assert.AreEqual(1L, (long)cmd1.ExecuteScalar()!, "Matches row should exist in SQLite");

                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(*) FROM MatchStats WHERE match_id = @id AND val_puuid = @puuid";
                cmd2.Parameters.AddWithValue("@id", matchId);
                cmd2.Parameters.AddWithValue("@puuid", EthanPuuid);
                Assert.AreEqual(1L, (long)cmd2.ExecuteScalar()!, "MatchStats row should exist for user");
            }
            SqliteConnection.ClearAllPools();

            Assert.IsTrue(server.RequestedPaths.Any(p => p.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase)), "Fake server must see matches endpoint");
        }

        [TestMethod]
        public async Task OutOfProcess_SameMatchPolledAgain_IsNotPostedTwice()
        {
            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            var (fixtureJson, _, _) = LoadRewrittenFixture(matchTime);

            await using var server = FakeHenrikServer.Start(fixtureJson);
            await using var app = AppProcess.Start(server.BaseUrl);

            await app.WaitForReadyAsync(TimeSpan.FromSeconds(60));
            await WaitForEmbedsAsync(app.RecordPath, 1, TimeSpan.FromSeconds(30));

            int countAfterFirst = server.RequestedPaths.Count(p => p.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase));
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(30))
            {
                int count = server.RequestedPaths.Count(p => p.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase));
                if (count >= countAfterFirst + 2 && sw.ElapsedMilliseconds >= 900)
                {
                    break;
                }
                await Task.Delay(50);
            }

            int finalCount = server.RequestedPaths.Count(p => p.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(finalCount >= countAfterFirst + 2, $"Expected at least {countAfterFirst + 2} matches requests, got {finalCount}");

            var embeds = ReadEmbedLines(app.RecordPath);
            Assert.AreEqual(1, embeds.Count, "rec.jsonl must still contain exactly one embed line");
        }

        [TestMethod]
        public async Task OutOfProcess_TestModeWithNonLoopbackUrl_ExitsNonZero()
        {
            await using var app = AppProcess.Start("https://api.henrikdev.xyz/valorant/", seedDatabase: false);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.Process.WaitForExitAsync(cts.Token);
            app.Process.WaitForExit();

            Assert.AreNotEqual(0, app.Process.ExitCode, "Process must exit with non-zero exit code.");
            string err = app.GetStderr();
            StringAssert.Contains(err.ToLowerInvariant(), "loopback", $"Stderr must mention 'loopback'. Actual stderr:\n{err}");
            Assert.IsFalse(File.Exists(app.RecordPath), "rec.jsonl must not exist.");
        }

        #endregion
    }
}
