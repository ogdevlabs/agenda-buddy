# AgendaMe verified app links

The current rollout is **iOS only**. Deploy this file with `Content-Type: application/json` and no
redirect:

- `apple-app-site-association` -> `https://agendame.app/.well-known/apple-app-site-association`

The Android HTTPS intent filter is deliberately disabled until a physical Android device and the Google
Play app-signing fingerprint are available. `assetlinks.todo.json` is a non-deployable template: do not
publish it as `/.well-known/assetlinks.json` while it contains the TODO marker.

## TODO: enable Android App Links

1. Obtain a physical Android device.
2. Obtain the Google Play app-signing SHA-256 fingerprint below.
3. Replace `TODO_GOOGLE_PLAY_APP_SIGNING_SHA256` and rename `assetlinks.todo.json` to `assetlinks.json`.
4. Build with `/p:AndroidEmailVerificationAppLinksEnabled=true`. The default is `false`; the property
	conditionally compiles the `https`/`agendame.app`/`/confirm-email` `IntentFilter` with
	`AutoVerify = true` into the APK manifest.
5. Deploy `assetlinks.json` to `https://agendame.app/.well-known/assetlinks.json`.
6. Install a Google Play-signed build on the physical device and run the verification commands below.

## Google Play signing fingerprint

Google Cloud Console does not create this fingerprint. It belongs to the certificate that signs the
installed Android application.

For a Google Play release:

1. Open Google Play Console.
2. Select **AgendaMe**.
3. Open **Setup > App integrity**.
4. Under **App signing key certificate**, copy **SHA-256 certificate fingerprint**.
5. Replace the TODO marker in `assetlinks.todo.json`, preserving the colon-separated format.

Use the **App signing key certificate**, not the upload-key certificate. Google Play re-signs the bundle
before distribution, so devices see the app-signing certificate.

For a directly installed build signed with a local keystore:

```bash
keytool -list -v -keystore /path/to/release.keystore -alias YOUR_ALIAS
```

Copy the `SHA256:` value. If both Play and direct distributions must verify, include both fingerprints in
`sha256_cert_fingerprints`.

## Verification

For the current iOS rollout:

```bash
curl -i https://agendame.app/.well-known/apple-app-site-association
```

After Android is enabled:

```bash
dotnet build AgendaBuddy.MobileApp/AgendaBuddy.MobileApp.csproj \
	-f net10.0-android \
	/p:AndroidEmailVerificationAppLinksEnabled=true
curl -i https://agendame.app/.well-known/assetlinks.json
adb shell pm verify-app-links --re-verify com.fererelabs.agendabuddy
adb shell pm get-app-links com.fererelabs.agendabuddy
```

Each HTTP response must be `200`, must not redirect, and must use `application/json`. Reinstall the iOS app
after changing the association file or entitlement because iOS caches Universal Link associations.
