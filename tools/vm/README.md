# Headless MW3 Windows VM route

These scripts use VirtualBox Guest Additions and `VBoxManage guestcontrol`. They do not move the host mouse, type on the host desktop, or require an interactive VirtualBox window. `Invoke-HeadlessGuest.ps1` creates `C:\MW3Lab` in a running Windows guest, copies a local PowerShell script and optional input files there, runs the script, and can copy named results back. It can also capture a VM screenshot. The guest account must be able to sign in and write that directory.

Keep the password file, game media, setup copies, screenshots, and result JSON under the installer's ignored `.local/` directory. The password file contains the guest password only and has no trailing newline. The script passes its path to VirtualBox; it does not print the password. Do not commit game media or copied installations.

## Capability and transfer check

From `MW3-Remastered-Installer/`, after booting an existing VirtualBox Windows guest headlessly:

```powershell
& tools/vm/Invoke-HeadlessGuest.ps1 `
  -VmName 'MW4 Joystick Lab 20260927' `
  -GuestUser 'MW4Lab' `
  -PasswordFile '.local/vm/password.txt' `
  -LocalScript 'tools/vm/Guest-Preflight.ps1' `
  -GuestArguments @('C:\MW3Lab\preflight.json') `
  -ResultFiles @('C:\MW3Lab\preflight.json') `
  -ResultDirectory '.local/vm/results' `
  -Offline
```

The listed VM and account are the existing lab example; supply the actual VM name and guest user for another machine. `-Offline` disconnects virtual network adapter 1 before transfer. Guest Additions transport still works offline; VirtualBox shared folders (`\\VBOXSVR`) may not. The JSON records OS, graphics adapter, display, 32-bit PowerShell, audio service, and optional installed-game file presence. It is a capability check, not evidence that MW3 renders or accepts input correctly.

To verify a setup transfer, add `-InputFiles @('dist/MechWarrior-3-Remastered-Setup.exe')` and pass a second empty argument and the guest file path: `-GuestArguments @('C:\MW3Lab\preflight.json', '', 'C:\MW3Lab\MechWarrior-3-Remastered-Setup.exe')`. Compare `InputFile.Sha256` in the JSON with `Get-FileHash` on the host. For an installed tree, pass its guest path as the second argument instead. `-InputFiles` accepts regular files; copy media only from user-owned, ignored local paths.

## Install and game checks

Use a dedicated disposable VM or clean snapshot for installer smoke. The existing MW4 lab can run the preflight in `C:\MW3Lab`, but do not overwrite its MW4 installations. Record the exact setup SHA-256, guest OS/build, GPU/driver, and selected media. Run the normal host build and deterministic smoke (`build.ps1`, `verify.ps1`) first. Then use a guest script for the intended MW3 install/launch check and retrieve its worker log and results. An elevated setup can stop at guest UAC: inspect the VM screenshot and approve only the expected prompt using VirtualBox VM input. The helper does not bypass UAC. A successful transfer or setup exit alone does not qualify rendering, audio, input, Pirate's Moon, uninstall, or repair.

`-ScreenshotPath` captures the guest display. If VirtualBox returns `E_FAIL` or the image is stale, the runner tries guest-side GDI. `Recover-HeadlessDisplay.ps1` sends Win+Ctrl+Shift+B to the VM only; capture the previous state first. A frozen guestcontrol session and frozen display may require recording the last log, then a VM-only reboot before retrying. Treat that run as interrupted evidence.