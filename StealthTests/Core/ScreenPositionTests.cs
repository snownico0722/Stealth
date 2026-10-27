
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Core;

namespace Stealth.Core.Tests
{
    [TestClass]
    public class ScreenPositionTests
    {
        [TestMethod]
        public void Clamp_RespectsNegativeCoordinateMonitors()
        {
            ScreenPosition.ClampToBounds(-2000, -60, 210, 40, -1920, -150, 0, 930,
                out int x, out int y);
            Assert.AreEqual(-1920, x);
            Assert.AreEqual(-60, y);
        }

        [TestMethod]
        public void Clamp_KeepsExpandedPanelInsideWorkingArea()
        {
            ScreenPosition.ClampToBounds(1830, 1050, 210, 40, 0, 0, 1920, 1080,
                out int x, out int y);
            Assert.AreEqual(1710, x);
            Assert.AreEqual(1040, y);
        }

        [TestMethod]
        public void Clamp_OversizedWindowStartsAtWorkingAreaOrigin()
        {
            ScreenPosition.ClampToBounds(500, 500, 500, 500, 0, 0, 300, 300,
                out int x, out int y);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
        }
    }
}
