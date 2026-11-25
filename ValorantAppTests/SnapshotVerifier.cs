using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace ValorantAppTests
{
    public static class SnapshotVerifier
    {
        public static void Verify(string snapshotName, Discord.Embed embed)
        {
            var recorded = RecordedEmbed.FromEmbed(embed);
            Verify(snapshotName, recorded);
        }

        public static void Verify(string snapshotName, object snapshotObj)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };

            string receivedJson = JsonConvert.SerializeObject(snapshotObj, settings) + "\n";
            receivedJson = receivedJson.Replace("\r\n", "\n");

            string snapshotsDir = GetSnapshotsDirectory();
            string approvedFileName = $"{snapshotName}.approved.json";
            string receivedFileName = $"{snapshotName}.received.json";

            string approvedPath = Path.Combine(snapshotsDir, approvedFileName);
            string receivedPath = Path.Combine(snapshotsDir, receivedFileName);

            bool updateSnapshots = Environment.GetEnvironmentVariable("VALORANTAPP_UPDATE_SNAPSHOTS") == "1";

            if (updateSnapshots)
            {
                File.WriteAllText(approvedPath, receivedJson, new UTF8Encoding(false));
                if (File.Exists(receivedPath))
                {
                    File.Delete(receivedPath);
                }

                // If running with build output directory separate from source tree, sync to output dir too
                string binDir = Path.Combine(AppContext.BaseDirectory, "Snapshots");
                if (Directory.Exists(binDir) && !string.Equals(Path.GetFullPath(binDir), Path.GetFullPath(snapshotsDir), StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(Path.Combine(binDir, approvedFileName), receivedJson, new UTF8Encoding(false));
                    string binReceived = Path.Combine(binDir, receivedFileName);
                    if (File.Exists(binReceived)) File.Delete(binReceived);
                }

                return;
            }

            if (!File.Exists(approvedPath))
            {
                // Fallback check in output directory if source was used
                string binApproved = Path.Combine(AppContext.BaseDirectory, "Snapshots", approvedFileName);
                if (File.Exists(binApproved))
                {
                    approvedPath = binApproved;
                }
                else
                {
                    File.WriteAllText(receivedPath, receivedJson, new UTF8Encoding(false));
                    Assert.Fail($"Approved snapshot file missing: '{approvedPath}'. Received file written to '{receivedPath}'. Set VALORANTAPP_UPDATE_SNAPSHOTS=1 to approve.");
                }
            }

            string approvedJson = File.ReadAllText(approvedPath, Encoding.UTF8).Replace("\r\n", "\n");

            if (approvedJson != receivedJson)
            {
                File.WriteAllText(receivedPath, receivedJson, new UTF8Encoding(false));
                Assert.Fail($"Snapshot mismatch for '{snapshotName}'.\nApproved: {approvedPath}\nReceived: {receivedPath}\nSet VALORANTAPP_UPDATE_SNAPSHOTS=1 to overwrite approved files.");
            }

            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }
        }

        public static string GetSnapshotsDirectory()
        {
            string sourceSnapshots = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Snapshots"));
            string projectFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../ValorantAppTests.csproj"));
            if (File.Exists(projectFile))
            {
                Directory.CreateDirectory(sourceSnapshots);
                return sourceSnapshots;
            }

            string binSnapshots = Path.Combine(AppContext.BaseDirectory, "Snapshots");
            Directory.CreateDirectory(binSnapshots);
            return binSnapshots;
        }
    }
}
