using System.Text;

namespace SideSpins.Api.Helpers
{
    public static class UlidGenerator
    {
        private static readonly Random Random = new Random();
        private static readonly char[] EncodingChars =
            "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

        public static string NewUlid()
        {
            return NewUlid(DateTime.UtcNow);
        }

        public static string NewUlid(DateTime timestamp)
        {
            var timestampBytes = BitConverter.GetBytes(timestamp.ToFileTimeUtc());
            var randomBytes = new byte[10];
            Random.NextBytes(randomBytes);

            // Combine timestamp (6 bytes) + random (10 bytes) = 16 bytes total
            var ulidBytes = new byte[16];
            Array.Copy(timestampBytes, 0, ulidBytes, 0, 6);
            Array.Copy(randomBytes, 0, ulidBytes, 6, 10);

            return EncodeBase32(ulidBytes);
        }

        private static string EncodeBase32(byte[] bytes)
        {
            var result = new StringBuilder(26);
            var buffer = 0L;
            var bitsLeft = 0;

            foreach (var b in bytes)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;

                while (bitsLeft >= 5)
                {
                    result.Append(EncodingChars[(int)(buffer >> (bitsLeft - 5)) & 31]);
                    bitsLeft -= 5;
                }
            }

            if (bitsLeft > 0)
            {
                result.Append(EncodingChars[(int)(buffer << (5 - bitsLeft)) & 31]);
            }

            return result.ToString();
        }
    }
}
