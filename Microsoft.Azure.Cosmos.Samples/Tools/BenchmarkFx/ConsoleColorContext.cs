//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;

    internal class ConsoleColorContext : IDisposable
    {
        ConsoleColor beforeContextColor;

        public ConsoleColorContext(ConsoleColor color)
        {
            this.beforeContextColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
        }

        public void Dispose()
        {
            Console.ForegroundColor = this.beforeContextColor;
        }
    }
}
