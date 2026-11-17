# Code signing policy

## Current status

This project's application to SignPath Foundation is **submitted and awaiting review**. On 17 November 2026, the application page was verified to show "Form submitted" and "Thank you, we'll be in touch soon." This confirms submission only. No application approval or production signing certificate has been recorded. Current test binaries are unsigned. The repository must not claim to be signed by SignPath before acceptance.

After approval, and only for releases actually signed through the approved service, the project will add the required attribution: “Free code signing provided by SignPath.io, certificate by SignPath Foundation.”

## Scope and roles

Only this project's `BNBU-DDL.exe` and `BNBU-DDL-Setup.exe` may be signed under a project policy. Microsoft libraries and runtime installers keep their original Microsoft signatures. The uninstaller is a byte-for-byte copy of the signed installer.

- Authors: repository owner [wangwangshu](https://github.com/wangwangshu), with any future contributors subject to review.
- Reviewers: [wangwangshu](https://github.com/wangwangshu), the current sole maintainer.
- Approvers: [wangwangshu](https://github.com/wangwangshu). Each release request requires an explicit manual review and approval; there is no separate review team at present.

Members involved in release approval must enable multifactor authentication on the source and signing accounts. Signing secrets belong in the CI secret store and never in the source repository. Fork pull requests must not receive signing credentials.

## Verifiable release sequence

1. A GitHub-hosted Windows job checks out the approved source revision.
2. It restores verified Microsoft dependencies and builds the application.
3. The application build artifact is submitted through SignPath's official GitHub integration and manually approved under the configured policy.
4. The returned signed application is embedded in the installer, without recompiling or editing it.
5. The installer artifact is separately submitted and manually approved.
6. `VerifySignedRelease.ps1` checks the installer signature, timestamp, and every embedded executable before the output is labelled signed.

The prepared signing workflow is disabled until `SIGNPATH_APPROVED=true` is deliberately configured after actual provider approval. It never falls back from a failed signed release to publishing unsigned files as signed.

See [PRIVACY.md](PRIVACY.md). The program does not transfer tasks to a project-operated service; portal access and Microsoft components are described there.
