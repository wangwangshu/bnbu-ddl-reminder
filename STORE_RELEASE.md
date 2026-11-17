# Microsoft Store release

Status: draft product created; not submitted, certified, signed, or publicly available yet.

The reserved product is **BNBU DDL Reminder**, Store ID `9N1NP999SXQ9`. Package identity values come from Partner Center's Product identity page and are recorded in `store/identity.json`. These are public package metadata, not certificates or signing credentials.

## Build

```powershell
./RestoreStoreDependencies.ps1
./BuildStore.ps1
```

Run on Windows 10 version 2004 or later, x64. The script verifies pinned NuGet dependency hashes, compiles the Store variant, runs isolated reminder/import checks, checks the payload against an exact allowlist, and builds an **unsigned** MSIX with Microsoft's MakeAppx semantic validation enabled. Outputs are in `dist/store`.

The MSIX is a submission input for Partner Center. It has no locally trusted signing certificate and is not a direct-download signed release. After Store certification, Microsoft signs and distributes the Store package. The old standalone EXE does not gain a signature from this process. See [Microsoft code signing options](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options).

## Store-specific behavior

- Uses a separate `BNBU-DDL-Store` data directory and process instance.
- Imports only JSON files explicitly selected by the user; does not scan Downloads or the old application's data.
- Declares a Windows startup task, disabled by default. The app's 开机提醒设置 button opens Windows Startup settings; the user can enable the task there. Startup launches reminders in the tray.
- Contains the app, startup launcher, WebView2 SDK DLLs, icons and notices. Does not include the old installer, runtime bootstrapper, uninstall scripts, certificates, tasks or browser profile.
- If WebView2 Runtime is absent, the app offers Microsoft's official download page. Reminders and manual tasks remain local.
- All Store package files are read-only at runtime; user data is kept in local application data.

## Submission checklist

1. Confirm the developer account's identity-verification and agreement status in Partner Center.
2. Create the first submission and upload `dist/store/BNBU-DDL-Store_3.1.0.0_x64.msix`.
3. Configure free pricing, desktop device availability, Education category, an accurate age rating, Chinese listing, screenshots, support URL and [privacy policy](https://github.com/wangwangshu/bnbu-ddl-reminder/blob/main/PRIVACY.md).
4. Explain `runFullTrust`: local WinForms tray reminders, selected-file import, and embedded school login via WebView2. No elevation, service, driver, antivirus exclusions or trust-store changes.
5. Reviewers can add a manual task and use 试一下提醒 without a school account. School synchronization requires a personal BNBU iSpace account and school network access; no user's school credentials are provided.
6. Validate a packaged installation, user-enabled startup and removal on a suitable Windows test device. Current checks of the unpackaged Store build do not establish that these packaged flows work.
7. Review the completed submission, then submit for certification. The app is not signed or available until Microsoft approves and processes it.

The project is an independent student tool and is not affiliated with, endorsed by, or an official client of BNBU.
