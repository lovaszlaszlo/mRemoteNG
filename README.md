# mRemoteNG — DBSystem fork

A personal fork of [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG), used every
working day to drive a few dozen RDP and SSH sessions. It is not a competing
project and not a rewrite. It is the same program, with the parts its author uses
every day made to work properly, and the parts nobody here has ever opened either
repaired or taken out.

It was written for one desk. It is published anyway, under the same GPL v2 as the
original: take it, run it, fork it again, you owe nobody anything. There is no
support, no roadmap, and no promise that tomorrow's commit will not break
something. What there is: it runs here all day, every day, and it is markedly
steadier than the build it grew out of.

---

## What is different

### A native SSH terminal

SSH no longer runs inside an embedded PuTTY window. A session is an
[xterm.js](https://xtermjs.org/) terminal hosted in WebView2, speaking to the
server over SSH.NET. That removes an entire class of problems that came from
gluing another program's top-level window into a tab — focus, Alt+Tab, resizing,
keyboard ownership and clipboard all now behave like the rest of the application.
Selecting text with the mouse copies it, clears the highlight and shows a tick, so
you know it happened. A failed connection says why and leaves the tab open, so you
can fix the password and retry where you are.

### Fullscreen for every protocol

Before, only RDP had one, and it belonged to the RDP control rather than to the
program. Now fullscreen hides the menu, the toolbars and both rows of tabs
whatever the session is. F11 works from inside a terminal too — where the web page
owns the keyboard — and acts on the window the session is actually in, so a
torn-off tab goes fullscreen itself instead of the window behind it.

### Tabs that come off and go back

A session can be sent to a window of its own and docked back from its right-click
menu. The docking library only ever offered this by dragging a window onto a drop
target that never appeared here. A session in a torn-off window still counts as
open, so double-clicking its connection goes to it rather than opening a second
one.

### Hungarian

The interface is translated to Hungarian at roughly two thirds. The rest falls
back to English. Both are selectable; nothing changed for the other languages.

### Steadier

The fixes that matter day to day, rather than a changelog:

- The properties panel no longer gives up building its grid.
- A connection fills the tab it sits in, instead of keeping the size it had
  before the tab grew.
- The window cannot end up with its title bar off every screen — which used to
  happen leaving fullscreen on a multi-monitor setup with mixed scaling, and the
  position was saved, so the next start came up the same way.
- The mouse cursor comes back if an RDP session left it hidden.
- Yes/no questions answer to the keyboard, Escape included.
- The splash screen opens on the screen the program itself will open on.

For the full list, see the [release notes](../../releases).

---

## Download

Releases are portable ZIPs: unpack and run, nothing is installed, the .NET runtime
is inside the package.

**[Latest release](../../releases/latest)** — Windows x64.

There is no MSI here. If you want an installer, upstream publishes one.

### Requirements

- Windows 10 or 11, x64.
- **The WebView2 runtime**, for the native SSH terminal. Windows 11 has it
  already. On a Windows 10 machine without it the SSH tab opens and tells you so —
  install the *Evergreen Standalone Installer* from Microsoft.
- Windows 8.1 will not work. WebView2 dropped it, and no amount of fixed-version
  packaging gets around the SDK's minimum.
- For RDP, the Microsoft Terminal Services client that ships with Windows.

### Your data

The released build is a **portable** one: its settings, connection file and log
sit **next to the executable**, not in `%APPDATA%`. Unpack it somewhere you can
write to — not `C:\Program Files` — and back the folder up like any other data.

It will not see the connections of an installed upstream mRemoteNG and will not
touch them. To bring them across, copy `confCons.xml` from `%APPDATA%\mRemoteNG`
into the unpacked folder. Make a copy of it first.

---

## Building it yourself

`dotnet build` **cannot** build this project: the csproj carries a COM reference
and `ResolveComReference` is not supported on .NET Core MSBuild. Use the Framework
MSBuild, and pass the platform explicitly or the solution picks arm64 and fails.

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  mRemoteNG\mRemoteNG.csproj -p:Configuration=Release -p:Platform=x64
```

For the self-contained portable package:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  mRemoteNG\mRemoteNG.csproj -p:Configuration="Release Self-Contained" -p:Platform=x64 `
  -p:PublishReadyToRun=false
```

`-p:PublishReadyToRun=false` is required; without it the publish step dies on a
runtime pack that was never restored. Output lands in
`mRemoteNG\bin\x64\Publish Self-Contained\`.

The test project does not compile, and did not before this fork either.

---

## Relationship to upstream

This fork diverged from `mRemoteNG/mRemoteNG` at commit `9211babf`, and is 109
commits ahead of it at the time of writing. Upstream work is looked at
periodically and taken across where it is worth having;
**[UPSTREAM-REVIEW.md](UPSTREAM-REVIEW.md)** records what was taken, what was
left, and why — because cherry-picking records the former and nothing of the
latter.

Nothing here has been offered back upstream. Most of it is opinionated in ways a
general-purpose project should not be.

## Reporting something

Bugs, questions, ideas and suggestions are welcome, with no expectation that any
of them get acted on quickly:

- **[Issues](../../issues)** — something is broken.
- **[Discussions](../../discussions)** — a question, an idea, a suggestion.

Both are reachable from the program's **Help** menu.

## Licence and credit

GPL v2, inherited from mRemoteNG — see [COPYING.txt](COPYING.txt).

Essentially all of this program was written by the
**[mRemoteNG team](https://github.com/mRemoteNG/mRemoteNG)** and the mRemote
authors before them, over many years. This fork is a thin layer of changes on top
of their work. See [CREDITS.md](CREDITS.md). If you want the maintained,
supported, installable version — with a community, documentation and people who
will answer you — go to them, and consider
[supporting them](https://mremoteng.org/contribute).

---
---

# Magyarul

Személyes fork az [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG)-ből, napi
munkára, néhány tucat RDP és SSH kapcsolathoz. Nem konkurens projekt és nem
újraírás: ugyanaz a program, csak azok a részei, amiket a szerzője nap mint nap
használ, rendesen működnek — amit meg soha senki nem nyitott meg itt, az vagy
megjavult, vagy kikerült.

Egy íróasztalra készült. Mégis közzétéve, ugyanazzal a GPL v2 licenccel, mint az
eredeti: vidd, használd, forkold tovább, senkinek nem tartozol semmivel. Nincs
támogatás, nincs ütemterv, és nincs ígéret arra, hogy a holnapi commit nem ront el
valamit. Ami van: itt fut egész nap, minden nap, és érezhetően stabilabb, mint az
a build, amiből nőtt.

## Mi más benne

**Natív SSH terminál.** Az SSH már nem beágyazott PuTTY-ablakban fut, hanem
[xterm.js](https://xtermjs.org/) terminálban, WebView2-ben, SSH.NET kapcsolaton.
Ezzel elmegy az a problémakör, ami abból jött, hogy egy másik program önálló
ablakát ragasztottuk egy fülbe: a fókusz, az Alt+Tab, az átméretezés, a
billentyűzet és a vágólap mostantól úgy viselkedik, mint a program többi része. Az
egérrel kijelölt szöveg a vágólapra kerül, a kijelölés eltűnik, és egy pipa
megmondja, hogy megtörtént. A sikertelen kapcsolat megmondja, miért, és a fül
nyitva marad.

**Teljes képernyő minden protokollhoz.** Eddig csak az RDP-nek volt ilyenje, és az
is az RDP vezérlőjéé volt. Az F11 a terminálon belülről is működik, és arra az
ablakra hat, amelyikben a munkamenet valóban van.

**A fülek önálló ablakba küldhetők és visszadokkolhatók** a jobbgombos menüből.

**Magyar felület**, nagyjából kétharmadáig lefordítva; a többi angolul marad.

**Stabilabb**: a Tulajdonságok panel nem adja fel a rács felépítését, a kapcsolat
kitölti a fülét, a címsor nem kerülhet minden képernyőn kívülre, az egérkurzor
visszajön egy RDP munkamenet után, az igen/nem kérdések billentyűzetről is
kezelhetők, és a splash ott jelenik meg, ahol a program is meg fog.

## Letöltés és követelmények

A kiadások hordozható ZIP-ek: kicsomagolod és indítod, telepíteni nem kell, a .NET
futtatókörnyezet benne van. **[Legutóbbi kiadás](../../releases/latest)** —
Windows x64.

A natív SSH-hoz kell a **WebView2 futtatókörnyezet**. Windows 11-en alapból ott
van; Windows 10-en, ha hiányzik, a program a fülön megmondja. Windows 8.1-en nem
működik.

**Az adataid** a hordozható csomagban az **exe mellé** kerülnek, nem a
`%APPDATA%`-ba. Oda csomagold ki, ahová írni tudsz, és mentsd a mappát, mint
bármilyen más adatot. A telepített upstream mRemoteNG kapcsolatait nem látja és
nem bántja; ha át akarod hozni őket, másold be a `confCons.xml`-t a
`%APPDATA%\mRemoteNG` mappából — előtte készíts róla másolatot.

## Bejelentés

**[Issues](../../issues)** — ha valami elromlott.
**[Discussions](../../discussions)** — kérdés, ötlet, javaslat. Mindkettő elérhető
a program **Súgó** menüjéből is.

## Licenc

GPL v2, az mRemoteNG-től örökölve — lásd [COPYING.txt](COPYING.txt).

Ennek a programnak gyakorlatilag az egészét az
**[mRemoteNG csapat](https://github.com/mRemoteNG/mRemoteNG)** írta, és előttük az
mRemote szerzői, hosszú évek alatt. Ez a fork egy vékony réteg a munkájukon. Ha
karbantartott, támogatott, telepíthető változatot akarsz — közösséggel,
dokumentációval és emberekkel, akik válaszolnak —, hozzájuk menj, és fontold meg,
hogy [támogatod őket](https://mremoteng.org/contribute).
