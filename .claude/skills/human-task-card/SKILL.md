---
name: human-task-card
description: Format something only the owner can do as a short, copy-paste task card. Use whenever work is blocked on a human - a credential, an account, a device check or a decision - instead of describing the blocker in prose.
---

# Human task card

The owner is not watching and should spend minutes, not an afternoon. Pick the **least manual
method that works**, in this order:

**Script → PowerShell → Browser UI → Manual**

1. **Script** — a committed script the owner runs once (see `tools/owner/` for the pattern).
2. **PowerShell** — a command to paste on the owner's Windows PC.
3. **Browser UI** — exact page and exact fields.
4. **Manual** — only when nothing above can do it (for example, putting on the headset).

## Card format

```markdown
### Human Task: <short title>

- **Goal:** <one sentence — what this unblocks>
- **Method:** <Script | PowerShell | Browser UI | Manual>
- **Time:** <minutes>

1. <step>
2. <step>
3. <step — five numbered steps at most>

**Done when:** <one observable condition a session can check without asking>
```

## Rules

- Five numbered steps at most. If it needs more, write a script and make running it step 1.
- `Done when:` must be observable from the repository, CI or an issue.
- **Never ask for a secret value in chat, an issue or a file.** The owner enters secrets in GitHub
  Actions secrets or the cloud environment; the card names the secret, never its value.
- **Never tell anyone to use the Unity Editor, a menu or the Inspector.** If a step needs a click
  in Unity, the work is not done — fix the tooling instead.
- Nothing on a card may cost money.

## Where the card goes

1. The body of an issue labelled `human-gate`.
2. One line in `INFO-REQUESTS.md`.
3. `blockedOnHuman` in `project.json` and **Blocked On Human** in `HANDOFF.md` — only if no other
   work package is unblocked. Otherwise leave both clear and take the next package.
