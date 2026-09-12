using System;

internal static class GameInstallRegistrySmoke
{
    private static int failures;

    private static void Main()
    {
        AssertRegistration(false, "MechWarrior 3", "1.2", false);
        AssertRegistration(true, "MechWarrior 3 EP1", "1.0", true);
        Assert(GameInstallRegistry.CanClaimRegistration(null, "C:\\Games\\MW3\\"), "an unregistered game should be claimable");
        Assert(GameInstallRegistry.CanClaimRegistration("C:\\Games\\MW3", "c:\\games\\mw3\\"), "the same path should be claimable regardless of case or trailing slash");
        Assert(!GameInstallRegistry.CanClaimRegistration("D:\\Original MW3\\", "C:\\Games\\MW3\\"), "an independent retail registration must be preserved");
        if (failures != 0) Environment.Exit(1);
        Console.WriteLine("Game install registry contract passed.");
    }

    private static void AssertRegistration(bool piratesMoon, string product, string version, bool expectsProgram)
    {
        GameInstallRegistration registration = GameInstallRegistry.Describe("C:\\Games\\MW3\\", piratesMoon);
        Assert(registration.MachineKeyPath == "SOFTWARE\\MicroProse\\" + product + "\\1.0", "wrong 32-bit machine key for " + product);
        Assert(registration.VirtualStoreKeyPath == "Software\\Classes\\VirtualStore\\MACHINE\\SOFTWARE\\WOW6432Node\\MicroProse\\" + product + "\\1.0", "wrong VirtualStore fallback key for " + product);
        Assert(registration.InstallPath == "C:\\Games\\MW3\\", "install path must contain exactly one trailing slash");
        Assert(registration.Version == version, "wrong version for " + product);
        Assert(registration.InstallOptions == 0x00050707, "incomplete InstallOptions for " + product);
        Assert((registration.Program != null) == expectsProgram, "wrong Program value policy for " + product);
    }

    private static void Assert(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL: " + message);
    }
}
