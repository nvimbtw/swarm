# Connector commands

All commands return JSON except interactive input and `serve`, which runs until stopped. A nonzero exit status can mean `pairing_required` or `pending`; inspect the JSON before deciding that a command failed.

```bash
swarm serve
swarm status
swarm connect --backend http://localhost:5142 --project swarm --name backend-codex --harness codex --workspace /absolute/project
swarm pair-start --backend http://localhost:5142
swarm pair-complete --backend http://localhost:5142 --code-stdin
swarm sessions --backend http://localhost:5142
swarm disconnect --backend http://localhost:5142 --session <swarm-session-id>
```

Feed the eight-digit pairing code through stdin, not a command-line flag. `pair-start` returns the exact `/swarm-pair pairing-id:...` command. The user runs it in Discord and receives a private code. Pairing challenges expire after five minutes and allow five incorrect code attempts.

`connect` saves backend, project, and mode in `<workspace>/.swarm/config.json`. Later commands can reuse these settings. Use `--workspace` when operating outside that directory. The project is currently a label; central project records do not exist yet. `--harness-session-id` is optional and enables reuse within the same workspace/project/harness; do not infer it from the workspace or display name. Without it, every `connect` creates a new registration.

One service can handle multiple backend origins, each authenticated separately. Use `swarm --home /path serve` and the same global `--home` argument on other commands to isolate state. The default is `~/.swarm`, overridable with `SWARM_HOME`.

If `swarm` is not installed, locate the Swarm checkout from the skill's resolved source path: this file lives under `<checkout>/skills/swarm-connect/references/`. Install with `python3 -m venv <checkout>/Client/.venv` and `<checkout>/Client/.venv/bin/python -m pip install -e <checkout>/Client`. Then use `<checkout>/Client/.venv/bin/swarm` explicitly. Do not assume the current project is the Swarm checkout.

The initial local transport uses a private Unix socket and supports Linux/macOS. The foreground service can be run in a separate shell or started using the user's preferred supervisor. It must outlive the short CLI call; a transient tool shell may terminate background children.

States: `registered` means acknowledged by the backend; `pending` means queued locally while connecting; `pairing_required` requires Discord verification; `rejected` means the backend refused the session. `authenticated` confirms credential issuance, not session registration. `swarm status` is local state; `swarm sessions` queries backend registrations owned by this Discord user.

The service reconnects and registers attached sessions again after transport loss. Service restarts discard session registrations but retain credentials; reconnect each live conversation. Backend restarts invalidate credentials until persistence is implemented. Disconnect explicitly when finished: automatic harness lifecycle tracking is not available.
