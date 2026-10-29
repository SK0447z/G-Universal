using System;

namespace GUniversal
{
    public static class ProviderFactory
    {
        public static ILaptopProvider CreateAutoDetectedProvider()
        {
            string brandAndModel = HardwareScanner.GetLaptopBrandAndModel();
            string lower = brandAndModel.ToLowerInvariant();

            if (lower.Contains("asus") || lower.Contains("asustek") || lower.Contains("rog") || lower.Contains("tuf") || lower.Contains("zenbook") || lower.Contains("zephyrus"))
            {
                return new AsusProvider();
            }

            if (lower.Contains("lenovo") || lower.Contains("legion") || lower.Contains("ideapad") || lower.Contains("thinkpad") || lower.Contains("loq"))
            {
                return new LenovoProvider();
            }

            if (lower.Contains("hp") || lower.Contains("hewlett") || lower.Contains("omen") || lower.Contains("victus") || lower.Contains("pavilion"))
            {
                return new HpProvider();
            }

            if (lower.Contains("acer") || lower.Contains("nitro") || lower.Contains("predator") || lower.Contains("aspire") || lower.Contains("helios"))
            {
                return new AcerProvider();
            }

            if (lower.Contains("dell") || lower.Contains("alienware"))
            {
                return new DellProvider();
            }

            return new GenericProvider();
        }
    }
}

