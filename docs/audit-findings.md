# DWMB Client — Code Audit Findings

Security- and stability-focused audit of the client. Each item is written to stand
alone as a work ticket (severity, location, problem, impact, suggested fix). File
references are `path:line` against the audited revision.

> **Note:** GitHub Issues is disabled on this repository, so these findings are
> tracked here instead. If Issues is enabled later, each section below maps 1:1 to
> an issue.

**Token sensitivity context:** the DWMB session token is disposable, regenerated
every session, and old tokens are invalidated/forgotten server-side. Leaking one
therefore has small, short-lived impact — reflected in the (low) severity of the
credential-hygiene items below.

---

## Critical — will crash or hang the app

### C1 — App freezes on Start when adapter count ≠ 1 (Console prompt in a GUI app)
**Severity:** Critical (stability) · **Where:** `MainWindow.xaml.cs:413–433` (`BeginCapture`)

Device selection auto-picks a device only when **exactly one** capture device is
found. Any other count (0, or 2+) falls into a `Console`-based prompt loop in a WPF
app that has no console:

```csharp
while (deviceNumber < 0 || deviceNumber >= connections.Count || !parseSuccess)
{
    string input = Console.ReadLine();   // returns null immediately — no console
    parseSuccess = int.TryParse(input, out deviceNumber);
}
```

`Console.ReadLine()` returns `null` instantly, `int.TryParse` fails forever, the loop
spins at 100% CPU. Since `btnStart_Click → MainApp → BeginCapture` runs on the UI
thread, the **whole app freezes**.

- **Impact:** Any machine with >1 adapter that has an IP (VPN, WSL, Hyper-V/VMware,
  WiFi + Ethernet) hits this on Start. The zero-device case (no Npcap, or no NIC with
  an IP) hits the same loop instead of a clean error.
- **Fix:** Replace the console prompt with a WPF device-selection dialog/ComboBox;
  handle the zero-device case with a user-facing error. Known TODO at `:413`.

- [ ] Resolved

### C2 — Missing/whitespace `server_location.txt` crashes at startup (field initializer)
**Severity:** Critical (stability) · **Where:** `ApiManager.cs:15`, `MessageForwarder.cs:8`

```csharp
private readonly string SERVER_ADDRESS = System.IO.File.ReadAllText("server_location.txt");
```

Runs in a **field initializer**, triggered when the static dummy `ApiManager` is
constructed during `MainWindow`'s constructor (via `DWMBClient.IsRegistered`). A
missing file surfaces as a `TypeInitializationException` and the app dies at launch
with a cryptic message. No `.Trim()`, so a trailing newline silently corrupts the
base URL.

- **Impact:** First-run / mis-deploy produces an ugly crash instead of guidance.
- **Fix:** Read the address lazily with a clear error path (catch
  `FileNotFoundException`, show a dialog). `.Trim()` and validate it is a well-formed
  absolute URI (see H3).

- [ ] Resolved

### C3 — Unhandled exception on the pcap capture thread terminates the app
**Severity:** Critical (stability) · **Where:** `MainWindow.xaml.cs:245–315` (`OnIncomingFsdPacket`)

Only the `am.ForwardMessage(...)` call is wrapped in try/catch. The parsing above it
is not: `PacketDotNet.Packet.ParsePacket(...)`, `Encoding.UTF8.GetString(...)`,
`new FsdMessage(...)`. Any exception there propagates on the SharpPcap callback
thread → unhandled → **process termination**. No global handler either (`App.xaml.cs`
is empty).

- **Impact:** A single malformed/unexpected packet can silently kill the client while
  the user believes they're protected.
- **Fix:** Wrap the body of `OnIncomingFsdPacket` in try/catch that logs and
  continues; add `DispatcherUnhandledException` / `AppDomain.UnhandledException`
  handlers in `App`.

- [ ] Resolved

---

## Medium

### M-H3 — No HTTPS enforcement; forwarded message content can travel in cleartext
**Severity:** Medium (security) · **Where:** `ApiManager.cs:15`, `:40`

`SERVER_ADDRESS` is whatever the config file contains, with no scheme validation. If
it is `http://`, every forwarded private/on-frequency message travels in cleartext
and is open to on-path tampering. The token angle here is minor (disposable token);
the real concern is **confidentiality and integrity of message content**.

- **Fix:** Require `https://` (reject/warn on `http://`); validate the URL when
  reading config.

- [ ] Resolved

### M1 — Message-forwarding failures are silently swallowed
**Severity:** Medium (stability/observability) · **Where:** `MainWindow.xaml.cs:304`

```csharp
catch (Exception ex)
{
    // TO DO - log this error.  But logger is not static so we can't access it here.
}
```

The catch is empty and its comment is wrong — `logger` **is** static in `DWMBClient`.
Server unreachable = messages dropped with no indication; UI still shows
`Capturing: true`. `lastMessage` is only updated on success, so dup-suppression after
a failure is inconsistent.

- **Fix:** Log the exception; consider a visible/aggregated error indicator or retry.

- [ ] Resolved

### M2 — Heartbeat timer keeps running after Pause
**Severity:** Medium (stability) · **Where:** `ApiManager.cs:230/250`; `MainWindow.xaml.cs:57`

The heartbeat timer is stopped only on Deregister, not on Stop/Pause. After pausing
capture, heartbeats keep firing, so the server still treats the non-capturing client
as online.

- **Fix:** Stop the heartbeat when capture is paused (and restart on resume), or
  otherwise reflect paused state to the server.

- [ ] Resolved

### M4 — Shared static state accessed from capture thread and UI thread without synchronization
**Severity:** Medium (stability) · **Where:** `MainWindow.xaml.cs:178`, `:282–302`, `:334`

`OnIncomingFsdPacket` runs on the SharpPcap background thread and reads/writes static
`lastMessage`, `am`, and `callsign`, while the UI thread mutates the same statics via
Start/Stop/Deregister. No locking → data race. Low blast radius today (dup
detection), but fragile.

- **Fix:** Guard shared state with a lock, or have the capture pipeline own its state
  and marshal UI updates via the Dispatcher.

- [ ] Resolved

### M5 — Capture can't be restarted; packet handler and device not released on Stop
**Severity:** Medium (stability) · **Where:** `MainWindow.xaml.cs:352`; TODO at `:57`

`Stop()` calls `device.StopCapture()` but never removes the `OnPacketArrival` handler
or disposes/closes the device. Capture can't be cleanly restarted (restart would
double-subscribe), and the device is leaked for the process lifetime. Known TODO.

- **Fix:** On Stop, unsubscribe `OnPacketArrival`, close/dispose the device, null it
  so a later Start re-initializes cleanly.

- [ ] Resolved

---

## Low — cleanup, dead code, hygiene

### L-cleanup — Dead code, latent crashes, and micro-inefficiencies
**Severity:** Low (cleanup) · Several items are in unused code that should be deleted
so it can't be revived with a latent bug.

- [ ] **`MessageForwarder.UploadMessage` — JSON injection** (`MessageForwarder.cs:32`).
  Builds JSON via `string.Format` from raw message content; a `"` or `\` breaks or
  injects into the payload. Unused (live path `ApiManager.ForwardMessage` uses
  `AddJsonBody`, which is safe). Prefer deleting `MessageForwarder` entirely.
- [ ] **`FsdPacket(byte[])` ctor — negative-length / bad header assumption**
  (`FsdPacket.cs:50–55`). `new byte[len - 54]` throws on packets < 54 bytes; the
  hardcoded 54-byte header breaks on IPv6/VLAN/TCP-options. Unused; delete or guard.
- [ ] **Duplicate `RestClient` allocation** (`ApiManager.cs:45–46`). First client is
  constructed then immediately discarded.
- [ ] **Regexes recompiled per packet** (`MainWindow.xaml.cs:334` and the four
  `Regex.Replace` packet-clean calls in `OnIncomingFsdPacket`). Make them
  `static readonly` or use `[GeneratedRegex]`.
- [ ] **`e.Data.ToString()`** at the top of `OnIncomingFsdPacket` returns a span type
  name, not data — harmless only because it's overwritten when a TCP payload exists;
  if `tcpPacket` is null the garbage string is processed.
- [ ] **`AddDefaultHeader` on the shared client each `TestConnection`**
  (`ApiManager.cs:127`) accumulates duplicate `User-Agent` headers on repeat calls.
- [ ] **Registration code shown in a plain `TextBox`** (`MainWindow.xaml`,
  `txtRegCode`). Low value to mask given the code is disposable and already shown in
  Discord in plaintext, but a `PasswordBox` would reduce shoulder-surfing.

### L-credhygiene — Session token written to log and sent in URLs
**Severity:** Low (security/hygiene). Downgraded after clarifying the token model
(disposable, regenerated each session, old tokens invalidated server-side), so
leaking one has small, short-lived impact. Filed for hygiene, not urgency.

- [ ] **Token written to `log.txt` in plaintext** (`MainWindow.xaml.cs:205`). Local
  file, ephemeral token — low impact, but avoid logging credentials by reflex.
  Redact/drop the reg code from the log line.
- [ ] **Token sent in URL query string / path** (`ApiManager.cs:62` register, `:214`
  heartbeat, `:98` deregister path). Lands in server/proxy access logs. Prefer a
  header or POST body.

Residual risk if intercepted mid-session: an attacker could deregister the user
(stopping notifications) or push spoofed pings — bounded to that one session.

---

## Noted as correct (load-bearing)

- Callsign is validated with `^(\d|\w|_|-)+$` (`MainWindow.xaml.cs:198`) **before** it
  is interpolated into a regex (`:334`), which prevents regex injection via callsign.
  This mitigation is load-bearing — if the pattern is relaxed, or the regex is ever
  built from the **server-returned** callsign (`am.Callsign`, which is not
  re-validated) instead of the validated input, the injection reopens.
- The live forward path (`ApiManager.ForwardMessage`) uses `AddJsonBody`, so it is not
  vulnerable to the JSON-injection issue that affects the dead `MessageForwarder`.
- TLS certificate validation is left at the system default (not disabled).
