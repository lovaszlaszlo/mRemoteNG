# Upstream review log

What was looked at from `mRemoteNG/mRemoteNG`, what was taken into this fork,
and what was left behind — with the reason. Cherry-picking tells git nothing
about what was skipped, so the next person to look at this list (most likely the
same two of us, a year from now) would otherwise have to judge all of it again
from scratch, and would have no way of knowing which decisions were deliberate.

Every commit taken is cherry-picked with `-x`, so its message carries the
original hash as well.

---

## 2026-09-15 — up to upstream build 3694/3695

**Fork point:** `9211babf` (PR #3406), the common ancestor of `dbsystem/main`
and `upstream/v1.78.2-dev`.
**Range reviewed:** `9211babf..upstream/v1.78.2-dev` — 122 commits.
**State at the time:** upstream 122 ahead, this fork 102 ahead.

### Read the release notes with care

The notes for build 3694 list everything since the **February** release (3405),
and this fork branched **after** that. So the headline items in them —
the SQL injection, command injection, XXE and path traversal fixes, the PuTTY
password file, OpenBao, 1Password, ARD, connection colours — **are already in
this fork**. Checked one by one with `git merge-base --is-ancestor`; all of them
are ancestors of the fork point.

Do not read those notes as a list of what is missing here. Compare commits.

### What the 122 commits actually were

| Kind | Count | Verdict |
|---|---|---|
| Dependency bumps (Renovate, AWS SDK, .NET monorepo, CEF…) | 32 | not taken |
| Empty "Initial plan" commits (Copilot) | 9 | nothing in them |
| Build, CI and installer (NB workflow, MSBuild bootstrap, WiX v5) | 19 | not taken |
| Test infrastructure | 2 | not taken |
| Actual product changes | 9 | see below |

### Taken

| Upstream | Here | What, and why |
|---|---|---|
| `fd511f0f` | `004331df` | Child hit testing for DPI-scaled click activation. `GetChildAtPoint` wants client coordinates and was handed `MousePosition`, which is in screen ones - so the further the window sits from the origin, the further out the hit test looks. This machine's second monitor starts at x=3840 and runs at a different scaling from the first, so it matters here more than most. **Conflicted**: upstream also simplifies the surrounding focus logic, which this fork has rewritten (Alt+Tab refocus, `DisableRefocus`, mouse-activation). Resolved by keeping our logic and taking only the coordinate conversion and the new `GetChildAtScreenPoint` helper. |
| `23852eb0` | `47fc879b` | Keep the active tab when a tab close is cancelled. Small, self-contained, in `DockPaneStripNG.cs` which this fork does not touch. Applied without conflict. |

### Not taken

| Upstream | What | Why not |
|---|---|---|
| `b2b23be2` | "Avoid RDP reconnect on resize for RDC8" | Not a fix - it **removes** the reconnect. The v8 client would keep its resolution when the window is resized instead of reconnecting at the new size. That is a daily-use behaviour change, and the decision was to keep the current behaviour. Revisit if resize-reconnect ever becomes the annoyance rather than the feature. |
| `4b128e99`, `019d591e`, `f296afd2` | Animated expand/collapse of the connection tree, with a new option on the Appearance page | Cosmetic, and it lands in `ConnectionTree.cs` and `AppearancePage`, both of which this fork has its own work in. Not worth the conflict for an animation. Take the three together if ever wanted - the second and third only make sense on top of the first. |
| `810b5db5` | "Refactor limit calculation for reveal limits" (3 lines in `ConnectionTree.cs`) | Belongs to the animation work above. Meaningless on its own. |
| `ce94b0a5` | "prefix warnings and small fixes" - 24 files | A mixed bag of warning cleanups that also carries their `AssemblyInfo` version numbering, which this fork deliberately left behind at 1.80. Would have to be unpicked line by line for very little. |
| `2ceb92c8` | Signed x64 `PuTTYNG.exe` embedded | This fork runs native SSH; PuTTY is not used. |
| 32 dependency bumps | Renovate traffic | Dependencies are updated here on our own schedule and with our own testing. Taking them blind would mean re-testing the whole program for someone else's timing. |
| 19 build/CI/installer commits | NB workflow, MSBuild resolution, WiX v5 migration | This fork builds locally with Framework MSBuild and ships a portable zip. None of their pipeline or installer is used. |
| 2 test commits | WinForms test harness | `mRemoteNGTests` does not compile in this tree (pre-existing `CS1503`), so there is nothing to gain. |

### Worth knowing for next time

- Upstream is **not** only bot traffic any more. Of 122 commits 9 were real
  product changes, and two of them were worth having - a far better ratio than
  the "only dependency bumps" this fork assumed.
- Their default branch is still `v1.78.2-dev` and every release is a
  **pre-release** nightly build. There has been no stable release since 1.78.2.
- The conflicts land exactly where the most work has been done here: `frmMain`,
  the connection tree, the options pages. That will get worse, not better.
- If this list ever gets long enough to be unmanageable, the answer is a real
  **merge** rather than more cherry-picking: a merge records that upstream
  commits are in this history even where the resulting file keeps our version,
  so later merges stop re-offering what was already judged. Cherry-picking
  records nothing, which is what this file is standing in for.
