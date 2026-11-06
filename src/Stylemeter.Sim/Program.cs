using System;
using System.Linq;

namespace Stylemeter.Sim
{
    internal static class Program
    {
        // "test" runs the rule checks; "replay <session.log>" plays a research log through the engine;
        // "sweep <setups> <log>..." replays logs under balancing setups ("sweep drain=1;drain=0.5,delay=1.5 a.log").
        private static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "replay")
                return Replay.Run(args[1]);
            if (args.Length >= 3 && args[0] == "sweep")
                return Replay.Sweep(args.Skip(2).ToList(), args[1]);
            if (args.Length == 0 || args[0] == "test")
                return Tests.Run();
            Console.WriteLine("usage: Stylemeter.Sim [test | replay <session.log> | sweep <setups> <session.log>...]");
            return 2;
        }
    }
}
