using NUnit.Framework;
using Stealth.Player;

namespace Stealth.Tests
{
    /// <summary>The texture size setting: what it skips, what the button cycles to, its labels.</summary>
    public class TextureSizeTests
    {
        [Test]
        public void TheSizeSkipsTopLevelsAndCycles()
        {
            Assert.AreEqual(0, TextureSizeRules.Skip(TextureSize.Full));
            Assert.AreEqual(1, TextureSizeRules.Skip(TextureSize.Half));
            Assert.AreEqual(2, TextureSizeRules.Skip(TextureSize.Quarter));
            Assert.AreEqual(TextureSize.Half, TextureSizeRules.Next(TextureSize.Full));
            Assert.AreEqual(TextureSize.Full, TextureSizeRules.Next(TextureSize.Quarter));
            Assert.AreEqual(TextureSizeRules.Default, TextureSizeRules.Clamp(7), "a stored value out of range: the default");
            Assert.AreEqual(TextureSize.Half, TextureSizeRules.Default, "1K by default: the 2K maps take about 1.8 GB of video memory");
            foreach (TextureSize s in System.Enum.GetValues(typeof(TextureSize)))
                StringAssert.StartsWith("menu.texture_size_", TextureSizeRules.Key(s));
        }
    }
}
