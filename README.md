# SPT 4.1.3 Phetzy Updater

Native Windows and Linux GUI installer and hotfix utility for the private SPT 4.1.3 friends pack.

## Download

Download the ZIP for your operating system from this repository's Releases page and extract it before running:

- Windows x64: `SPT413-Phetzy-Updater.exe`
- Linux x64: `SPT413-Phetzy-Updater.Linux`

On Linux, make the downloaded file executable if the archive tool did not preserve its mode:

```bash
chmod +x SPT413-Phetzy-Updater.Linux
./SPT413-Phetzy-Updater.Linux
```

The official release embeds access to the private pack. No separate configuration or AWS credentials are required.

The updater checks the latest GitHub release when it opens. It selects the Windows or Linux asset automatically, then can download, verify, replace, and restart itself. A commit alone does not trigger client updates; publish a newer versioned release.

Use **Apply Hotfix** to update Item Preview QoL in an existing compatible installation.

## Full-pack installation

1. Start with a fresh combined SPT 4.1.3 installation backed by EFT build 40743.
2. Close EFT, the launcher, the server, and any headless clients using that installation.
3. Run the updater and select the SPT installation folder.
4. Select **Fresh install from private pack**.

The updater validates the target version and existing-mod state. It downloads the AWS-hosted pack, reports progress, verifies SHA-256, installs the archives, audits the receipt, and deletes its downloaded bundle after success.

## Build

Use the .NET 10 SDK:

```powershell
dotnet test .\cross-platform.tests\SPT413-Phetzy-Updater.CrossPlatform.Tests.csproj -c Release
dotnet publish .\cross-platform\SPT413-Phetzy-Updater.CrossPlatform.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish .\cross-platform\SPT413-Phetzy-Updater.CrossPlatform.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

Release builds supply `PrivateManifestSource` and `HotfixPayload` MSBuild properties. The repository workflow reads the private source from the `PHETZY_UPDATER_SOURCE_JSON_B64` Actions secret.

The Item Preview QoL hotfix payload is included under its MIT license. See `THIRD_PARTY_LICENSES/ItemPreviewQoL-MIT.txt`.

## Security

The official release embeds bearer access to the private pack. Do not mirror or repost the updater. Local source builds without the private embedded resource can still use an adjacent `updater-source.json` for development.

The executables are not code-signed. Verify the release ZIP against its accompanying `.sha256` file before running it.
