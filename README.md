# SPT 4.1.3 Phetzy Updater

Windows GUI installer and hotfix utility for the private SPT 4.1.3 friends pack.

## Download

Download the latest ZIP from this repository's Releases page. Extract the complete ZIP before running `SPT413-Phetzy-Updater.exe`.

The private `updater-source.json` access file is deliberately excluded from GitHub. Obtain it from the pack owner and place it beside the EXE before selecting **Fresh install from private pack**.

The **Apply inspect-rotation hotfix** operation does not require the access file.

The updater checks the latest GitHub release when it opens. It can download, verify, replace, and restart itself when a newer release is available. The private `updater-source.json` file remains in place. A commit alone does not trigger client updates; publish a newer versioned release.

## Full-pack installation

1. Start with a fresh combined SPT 4.1.3 installation backed by EFT build 40743.
2. Close EFT, the launcher, the server, and any headless clients using that installation.
3. Place the private `updater-source.json` beside the extracted EXE.
4. Run the updater and select the SPT installation folder.
5. Select **Fresh install from private pack**.

The updater validates the target version and existing-mod state. It downloads the AWS-hosted pack, reports progress, verifies SHA-256, installs the archives, audits the receipt, and deletes its downloaded bundle after success.

## Build

Use the .NET 10 SDK on Windows:

```powershell
dotnet publish .\src\SPT413-Phetzy-Updater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The Item Preview QoL hotfix payload is included under its MIT license. See `THIRD_PARTY_LICENSES/ItemPreviewQoL-MIT.txt`.

## Security

Never commit or publish `updater-source.json`. It contains bearer access to the private pack.

The executable is not Authenticode-signed. Verify the release asset against its accompanying `.sha256` file before running it.
