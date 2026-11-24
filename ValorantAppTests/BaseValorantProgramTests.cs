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

            program.Start();
            program.Start(); // second call is a no-op
        }
    }
}
