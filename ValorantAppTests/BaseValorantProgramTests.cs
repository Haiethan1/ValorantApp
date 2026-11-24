using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Net.Http;
using System.Threading.Tasks;
using ValorantApp.Valorant;

namespace ValorantAppTests
{
    /// <summary>
    /// Seam 3: the program can be built without starting timers, and a poll tick / daily run can be driven
    /// on demand. Uses the temp SQLite DB (no users seeded) and a recording notifier.
    /// </summary>
    [TestClass]
    public class BaseValorantProgramTests
    {
        private static (BaseValorantProgram Program, RecordingNotifier Notifier) CreateProgram()
        {
            var notifier = new RecordingNotifier();
            var program = new BaseValorantProgram(
                notifier,
                new Mock<IHttpClientFactory>().Object,
                NullLogger<BaseValorantProgram>.Instance);
            return (program, notifier);
        }

        [TestMethod]
        public async Task RunPollTickAsync_WithNoUsers_CompletesAndPostsNothing()
        {
            var (program, notifier) = CreateProgram();

            await program.RunPollTickAsync();

            Assert.AreEqual(0, notifier.Sent.Count);
        }

        [TestMethod]
        public async Task RunDailyCheckAsync_UpdatesBotStatusThroughNotifier()
        {
            var (program, notifier) = CreateProgram();

            await program.RunDailyCheckAsync();

            Assert.AreEqual(1, notifier.Statuses.Count);
            StringAssert.StartsWith(notifier.Statuses[0], "Matches Found ");
        }

        [TestMethod]
        public void Start_CanBeCalledTwice()
        {
            var (program, _) = CreateProgram();

            // The timers are private with no accessors, so read them by reflection to prove the second call
            // is a no-op (same timer instances) rather than starting a second set of timers.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var pollField = typeof(BaseValorantProgram).GetField("ScheduledMessageTimer", flags)!;
            var dailyField = typeof(BaseValorantProgram).GetField("DailyCheckTimer", flags)!;

            program.Start();
            object? pollTimer = pollField.GetValue(program);
            object? dailyTimer = dailyField.GetValue(program);
            Assert.IsNotNull(pollTimer, "Start should create the poll timer");
            Assert.IsNotNull(dailyTimer, "Start should create the daily timer");

            program.Start(); // second call is a no-op

            Assert.AreSame(pollTimer, pollField.GetValue(program), "Second Start must not replace the poll timer");
            Assert.AreSame(dailyTimer, dailyField.GetValue(program), "Second Start must not replace the daily timer");
        }
    }
}
