namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    public static class DeBruijn64Bits
    {
        private static readonly int[] BitPatternToLog2 = new int[64] {
            0, // change to 1 if you want bitSize(0) = 1
            1,  2, 53,  3,  7, 54, 27, 4, 38, 41,  8, 34, 55, 48, 28,
            62,  5, 39, 46, 44, 42, 22,  9, 24, 35, 59, 56, 49, 18, 29, 11,
            63, 52,  6, 26, 37, 40, 33, 47, 61, 45, 43, 21, 23, 58, 17, 10,
            51, 25, 36, 32, 60, 20, 57, 16, 50, 31, 19, 15, 30, 14, 13, 12
        }; // table taken from http://chessprogramming.wikispaces.com/De+Bruijn+Sequence+Generator

        private const ulong Multiplicator = 0x022fdd63cc95386dUL;

        private static int BitSize(ulong v)
        {
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            // at this point you could also use popcount to find the number of set bits.
            // That might well be faster than a lookup table because you prevent a 
            // potential cache miss
            if (v == unchecked((ulong)-1)) return 64;
            v++;
            return BitPatternToLog2[(ulong)(v * Multiplicator) >> 58];
        }

        public static int GetLeadingZeroCount(long value)
        {
            return 64 - BitSize((ulong)value);
        }
    }

    public static class DeBruijn64BitsBitScanner
    {
        private const ulong Magic = 0x37E84A99DAE458F;

        private static readonly int[] MagicTable =
            {
            0, 1, 17, 2, 18, 50, 3, 57,
            47, 19, 22, 51, 29, 4, 33, 58,
            15, 48, 20, 27, 25, 23, 52, 41,
            54, 30, 38, 5, 43, 34, 59, 8,
            63, 16, 49, 56, 46, 21, 28, 32,
            14, 26, 24, 40, 53, 37, 42, 7,
            62, 55, 45, 31, 13, 39, 36, 6,
            61, 44, 12, 35, 60, 11, 10, 9,
        };

        public static int BitScanForward(ulong b)
        {
            return MagicTable[((ulong)((long)b & -(long)b) * Magic) >> 58];
        }

        private static int BitScanReverse(ulong b)
        {
            b |= b >> 1;
            b |= b >> 2;
            b |= b >> 4;
            b |= b >> 8;
            b |= b >> 16;
            b |= b >> 32;
            b = b & ~(b >> 1);
            return MagicTable[b * Magic >> 58];
        }

        public static int GetLeadingZeroCount(long value)
        {
            return 63 ^ BitScanReverse((ulong)value);
        }
    }

    //https://chessprogramming.wikispaces.com/BitScan
    public static class DeBruijnMultiplication
    {
        private static readonly int[] Index64 = new int[64] {
            0, 47,  1, 56, 48, 27,  2, 60,
           57, 49, 41, 37, 28, 16,  3, 61,
           54, 58, 35, 52, 50, 42, 21, 44,
           38, 32, 29, 23, 17, 11,  4, 62,
           46, 55, 26, 59, 40, 36, 15, 53,
           34, 51, 20, 43, 31, 22, 10, 45,
           25, 39, 14, 33, 19, 30,  9, 24,
           13, 18,  8, 12,  7,  6,  5, 63
        };

        /**
         * bitScanReverse
         * @authors Kim Walisch, Mark Dickinson
         * @param bb bitboard to scan
         * @precondition bb != 0
         * @return index (0..63) of most significant one bit
         */
        private static int BitScanReverse(ulong bb)
        {
            const ulong debruijn64 = (0x03f79d71b4cb0a89);
            //assert(bb != 0);
            bb |= bb >> 1;
            bb |= bb >> 2;
            bb |= bb >> 4;
            bb |= bb >> 8;
            bb |= bb >> 16;
            bb |= bb >> 32;
            return Index64[(bb * debruijn64) >> 58];
        }
        public static int GetLeadingZeroCount(long value)
        {
            return 63 ^ BitScanReverse((ulong)value);
        }
    }
}