# Dock to Drop: project rules

## Session limits (the user is on the Pro plan)

- One narrowly scoped shot or fix per session.
- One preview and at most one correction pass. Then return the result and the remaining issues to the user.
- No background agents, side sessions or repeated polling unless the user asks for them.
- Inspect selected frames, not entire frame sequences. Keep tool output short.
- Reuse existing assets. Don't repeat research that is already done (see `ASSET_DOWNLOADS.md`, `HANDOFF.md`).
- Keep `STATE.md` under 300 words, with the exact next step, so a fresh session can continue.
- Don't change models, effort, global settings or connectors without asking.

## Where things are

- `STATE.md`: current state and the next step. Read it first.
- `HANDOFF.md`: longer history and the ranked "Open" list.
- Only one session at a time may edit `unity-assets/Scripts/WorldBuilder.cs` or run `./prepare_unity.sh && ./build_mac.sh`.
