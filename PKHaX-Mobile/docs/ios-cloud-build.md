# iOS without a Mac — one-time setup, then push-to-build

You have a **paid** Apple Developer account but no Mac. That's all you need: the certificate is made with
`openssl` on Linux, everything else is done on the Apple website, and the actual iOS build runs on GitHub's
macOS cloud runner (the workflow at `.github/workflows/build-mobile.yml`). You install the finished `.ipa`
onto your iPhone over USB from Linux — no Mac at any step.

Do steps 1–7 **once**. After that, every push (or one click) rebuilds iOS in the cloud.

Run all local commands in a throwaway folder OUTSIDE the git repo so keys never get committed:

```bash
mkdir -p ~/keys/pkhax && cd ~/keys/pkhax
```

---

## 1. Get your iPhone's UDID (Linux, over USB)

```bash
sudo apt install -y libimobiledevice-utils      # one-time; provides idevice_id / ideviceinstaller
# plug the iPhone in, tap "Trust", then:
idevice_id -l
```

That 40-char (or 25-char with a dash) string is your **UDID**. (No Linux box handy? On Windows, open the
iPhone in iMazing or iTunes and copy the UDID from the device info panel.)

## 2. Register the app + device on the Apple portal (web, no Mac)

At <https://developer.apple.com/account> → **Certificates, Identifiers & Profiles**:

- **Identifiers → +** → App IDs → App → **explicit Bundle ID `com.unnerf.pkhax`** (must match the app). Leave
  all capabilities unchecked. Register.
- **Devices → +** → paste the UDID from step 1, give it a name. Register.

## 3. Make a signing key + CSR with openssl (Linux — replaces a Mac's Keychain)

```bash
cd ~/keys/pkhax
openssl genrsa -out ios_dist.key 2048
openssl req -new -key ios_dist.key -out ios_dist.csr \
  -subj "/emailAddress=tirelessgolem@gmail.com/CN=PKHaX Distribution/C=US"
```

Keep `ios_dist.key` private — it's half of your identity.

## 4. Create the Apple **Distribution** certificate (web)

Portal → **Certificates → +** → **Apple Distribution** → upload `ios_dist.csr` → Continue → **Download**
`distribution.cer` into `~/keys/pkhax`. (Ad-hoc installs use the *Distribution* cert, not Development.)

## 5. Convert the cert to a `.p12` and read your signing identity (Linux)

```bash
cd ~/keys/pkhax
openssl x509 -inform DER -in distribution.cer -out distribution.pem
# -legacy makes the .p12 readable by the macOS runner's `security import` on every runner version:
openssl pkcs12 -export -legacy -inkey ios_dist.key -in distribution.pem \
  -out ios_dist.p12 -name "Apple Distribution" -passout pass:CHANGE_THIS_P12_PASSWORD
# print the exact signing identity string (the CN line):
openssl x509 -in distribution.pem -noout -subject
```

- The password you set (`CHANGE_THIS_P12_PASSWORD`) becomes the secret **IOS_CERTIFICATE_P12_PASSWORD**.
- The printed subject `CN = Apple Distribution: Your Name (TEAMID)` (drop the `CN = `) is the secret
  **IOS_SIGNING_IDENTITY**.

## 6. Create the Ad Hoc provisioning profile (web)

Portal → **Profiles → +** → **Ad Hoc** (under Distribution) → App ID `com.unnerf.pkhax` → pick the Apple
Distribution cert from step 4 → tick your device from step 2 → **Name** it e.g. `PKHaX AdHoc` (this Name is
the secret **IOS_PROVISIONING_PROFILE_NAME**) → Generate → **Download** the `.mobileprovision` into
`~/keys/pkhax`.

## 7. Base64 the two files and add the 5 GitHub secrets

```bash
cd ~/keys/pkhax
base64 -w0 ios_dist.p12         > ios_dist.p12.b64
base64 -w0 *.mobileprovision    > ios_profile.b64
```

In the repo → **Settings → Secrets and variables → Actions → New repository secret**, add:

| Secret | Value |
|---|---|
| `IOS_CERTIFICATE_P12_BASE64` | contents of `ios_dist.p12.b64` |
| `IOS_CERTIFICATE_P12_PASSWORD` | the password from step 5 |
| `IOS_PROVISIONING_PROFILE_BASE64` | contents of `ios_profile.b64` |
| `IOS_PROVISIONING_PROFILE_NAME` | the profile Name from step 6 (e.g. `PKHaX AdHoc`) |
| `IOS_SIGNING_IDENTITY` | e.g. `Apple Distribution: Your Name (TEAMID)` |

---

## Build it (every time)

The workflow triggers on any push to `main` that touches the app, or run it by hand:

```bash
gh workflow run build-mobile.yml -R isleep2late/Un-Nerf-Compendium
sleep 6
RID=$(gh run list -R isleep2late/Un-Nerf-Compendium -w build-mobile.yml -L1 --json databaseId -q '.[0].databaseId')
gh run watch "$RID" -R isleep2late/Un-Nerf-Compendium
```

(The workflow must exist on `main` for `workflow_dispatch` to work — it ships to `main` through the normal
compendium deploy button. Or use the **Actions** tab → *Build PKHaX Mobile* → *Run workflow*.) First run is
slowest (~15–30 min: it installs the workloads). It produces two artifacts: `pkhax-android-apk` and
`pkhax-ios-ipa`.

## Install the iOS build on your iPhone (Linux, over USB — no Mac, no re-sign)

```bash
gh run download "$RID" -R isleep2late/Un-Nerf-Compendium -n pkhax-ios-ipa
# plug the iPhone in (trusted), then:
ideviceinstaller -i PKHaX.ipa
```

The `.ipa` is already ad-hoc signed for your iPhone's UDID, so `ideviceinstaller` installs it as-is and the
signature stays valid ~1 year. (On Windows instead: **iMazing → Install app from .ipa file**, "install as-is".
Avoid AltStore/Sideloadly's default mode — it re-signs with a free Apple ID and cuts validity to 7 days.)

## Renewals / limits (worth knowing)

- The Apple Distribution certificate and the Ad Hoc profile each last **1 year**; regenerate them (steps 3–6)
  when they expire and update the secrets.
- Ad hoc allows up to **100 devices** per membership year. To add another iPhone: register its UDID (step 1–2),
  regenerate the profile (step 6), update `IOS_PROVISIONING_PROFILE_BASE64`, rebuild.
- Keep `~/keys/pkhax` backed up privately. Never commit it (it's outside the repo by design).
