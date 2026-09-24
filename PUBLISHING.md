# Publishing a release

## Repository

Create the public repository from this directory and push the committed source. Generated setup binaries are ignored so repeated releases do not permanently inflate Git history.

## Release checklist

1. Review `THIRD_PARTY_NOTICES.md` and confirm redistribution evidence for every payload input.
2. Run `build.ps1` and `verify.ps1` on the maintained Windows build machine.
3. Run `verify.ps1 -KeepSmokeTree`, then scan the setup EXE, `smoke-test/payload`, and the complete `smoke-test` tree with current Microsoft Defender definitions. Confirm that no new event 1116 appears in the Defender operational log before publishing.
4. Confirm that `dist/SHA256SUMS.txt` matches the EXE and review `dist/PAYLOAD_MANIFEST.sha256`.
5. Tag the source commit with the same semantic version shown by the EXE.
6. Create a GitHub Release from that tag.
7. Upload all three generated files from `dist` as release assets without renaming or modifying them.
8. Publish release notes listing meaningful changes, test coverage, known limitations, and the checksum.

After GitHub CLI authentication, a release can be created with a command shaped like:

```powershell
gh release create v1.2.7 .\dist\MechWarrior-3-Remastered-Setup.exe .\dist\SHA256SUMS.txt .\dist\PAYLOAD_MANIFEST.sha256 --title "MechWarrior 3 Remastered v1.2.7" --notes-file .\release-notes-v1.2.7.md
```

Never replace assets attached to an existing release. Publish a new version so old checksums remain meaningful.
