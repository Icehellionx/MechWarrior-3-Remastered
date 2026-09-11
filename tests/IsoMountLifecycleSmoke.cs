using System;
using System.IO;

internal static class IsoMountLifecycleSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: IsoMountLifecycleSmoke <iso-path>");
            return 2;
        }

        try
        {
            using (IsoMountSession session = IsoMountSession.Attach(args[0], Console.WriteLine))
            {
                if (!session.OwnsMount) throw new InvalidOperationException("The clean test did not own its mount.");
                if (String.IsNullOrEmpty(session.Root) || !Directory.Exists(session.Root))
                    throw new InvalidOperationException("The mounted disc root is not readable.");
                Console.WriteLine("Mounted disc root is readable: " + session.Root);
            }
            Console.WriteLine("ISO mount lifecycle smoke passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
