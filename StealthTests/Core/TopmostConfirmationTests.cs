using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Model;

namespace Stealth.Model.Tests
{
    [TestClass]
    public class TopmostConfirmationTests
    {
        [TestMethod]
        public void SecondCallOnlyAppliesAfterTargetProcessesIt()
        {
            bool actual = false;
            bool queuedSecondRequest = false;
            int requests = 0;
            var verification = new TopmostConfirmation(true);
            Assert.AreEqual(TopmostConfirmationResult.RetryIssued,
                verification.Observe(actual, () => { requests++; queuedSecondRequest = true; }));
            Assert.AreEqual(1, requests);
            Assert.IsTrue(queuedSecondRequest);
            // Simulate Task Manager taking time to handle the retry.
            actual = true;
            Assert.AreEqual(TopmostConfirmationResult.Confirmed,
                verification.Observe(actual, () => requests++));
            Assert.AreEqual(1, requests);
        }

        [TestMethod]
        public void FailedAttemptCanRetryOnlyOnce()
        {
            var verification = new TopmostConfirmation(true);
            int requests = 0;
            Assert.AreEqual(TopmostConfirmationResult.RetryIssued,
                verification.Observe(false, () => requests++));
            Assert.AreEqual(TopmostConfirmationResult.Failed,
                verification.Observe(false, () => requests++));
            Assert.AreEqual(1, requests);
        }

        [TestMethod]
        public void AlreadyAppliedWindowNeverReceivesExtraCalls()
        {
            var verification = new TopmostConfirmation(true);
            int requests = 0;
            Assert.AreEqual(TopmostConfirmationResult.Confirmed,
                verification.Observe(true, () => requests++));
            Assert.AreEqual(0, requests);
        }

        [TestMethod]
        public void UnpinWorksWithTheSameBoundedPolicy()
        {
            var verification = new TopmostConfirmation(false);
            int requests = 0;
            Assert.AreEqual(TopmostConfirmationResult.RetryIssued,
                verification.Observe(true, () => requests++));
            Assert.AreEqual(TopmostConfirmationResult.Confirmed,
                verification.Observe(false, () => requests++));
            Assert.AreEqual(1, requests);
        }
    }
}
