using NUnit.Framework;
using Stealth.Player;

namespace Stealth.Tests
{
    public class SettingsTests
    {
        [TearDown]
        public void Restore()
        {
            GameSettings.ResetToDefaults();
            GameSettings.Save();
        }

        [Test]
        public void SaveLoadRoundTrip()
        {
            GameSettings.Sensitivity = 0.33f;
            GameSettings.Fov = 90f;
            GameSettings.InvertY = true;
            GameSettings.HeadBob = false;
            GameSettings.CrouchToggle = true;
            GameSettings.Save();
            GameSettings.ResetToDefaults();
            GameSettings.Load();
            Assert.AreEqual(0.33f, GameSettings.Sensitivity, 1e-4f);
            Assert.AreEqual(90f, GameSettings.Fov, 1e-4f);
            Assert.IsTrue(GameSettings.InvertY);
            Assert.IsFalse(GameSettings.HeadBob);
            Assert.IsTrue(GameSettings.CrouchToggle);
        }

        [Test]
        public void LoadClampsBrokenValues()
        {
            UnityEngine.PlayerPrefs.SetFloat("stealth.fov", 500f);
            UnityEngine.PlayerPrefs.SetFloat("stealth.sens", -3f);
            GameSettings.Load();
            Assert.AreEqual(100f, GameSettings.Fov);
            Assert.AreEqual(0.01f, GameSettings.Sensitivity, 1e-5f);
        }
    }
}
