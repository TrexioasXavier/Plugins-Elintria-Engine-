# Plugins

Packages add features to a project: libraries your scripts can call, content,
editor tools. A project starts with none, and nothing is installed unless you
ask for it — including the packages that ship with the engine.

Open **Window ▸ Plugins**.

```
Packages installed in this project
C:\MyGame\Plugins
[Install from Folder] [Install from Zip] [Refresh]

Install from a link or a path
[ https://github.com/owner/repo                          ] [Install]

Installed (1)
 ☑ ENetworking 1.0.0                      [Samples] [Uninstall]
   Elintria - TCP and UDP networking...   [adds the networking tab]
```

## Installing

Four ways in, all landing in the same place:

| | |
|---|---|
| **Install from Folder** | pick a folder holding a `plugin.json` |
| **Install from Zip** | pick a `.zip` holding one, at its root or one level down |
| a **GitHub link** | `https://github.com/owner/repo` downloads the default branch; `.../tree/branch` takes that branch |
| a **path** | paste a folder or `.zip` path into the same box |

Packages install into `<project>/Plugins/<name>/`, so they travel with the
project and a package you tried once never turns up in an unrelated one.
Reinstalling replaces, which is how you update.

The **checkbox** turns a package off without deleting it: its headers leave
your script project, its DLLs stop shipping, and any editor tab it added goes
back to explaining itself. **Uninstall** asks once, then deletes the folder.

## What a package can do

- put **headers** on your script project's include path
- **link a library** into your compiled scripts
- ship **DLLs** that are copied next to your built game
- add **preprocessor defines** for your scripts
- contribute **C# sources**, compiled into your project's script assembly -
  so your scripts can use what they declare with no reference to add. They do
  not appear in Add Component: only concrete script classes do, and a library
  type is not one
- offer **sample scripts**, imported into `Scripts/` only when you press
  Samples, and never overwriting a file you already have
- light up an **editor tab** the editor already knows how to draw

## What a package cannot do

**Run anything at install time.** There is no install script, no build step,
no post-install hook. Installing is a file copy and a manifest read, so adding
a package from a link cannot by itself do anything to your machine. Code in a
package runs when you build a game with it, or when the editor loads a DLL for
a feature you switched on — both visible, both your choice.

**Bring its own editor UI.** `editorFeature` names a feature the editor
already implements - `networking`, or `unityimport` - and an unrecognised
name is ignored rather than trusted. This
is a deliberate limit: a package supplying C++ UI would mean STL types across
a DLL boundary, which this engine has been bitten by before.

**Escape its own folder.** Every path in a manifest is checked — absolute
paths, drive letters and `..` are rejected — so a package cannot point the
compiler or the build copier at somewhere else on disk.

None of that makes an untrusted package safe. A package you build a game with
gets to run code in that game. Install ones you would run.

## Writing one

A package is a folder with `plugin.json` at its root:

```json
{
  "name": "MyPackage",
  "displayName": "My Package",
  "version": "1.0.0",
  "author": "You",
  "description": "One line, shown in the list.",
  "website": "https://github.com/you/mypackage",
  "readme": "README.md",

  "include": ["include"],
  "libDirs": ["lib/Win32"],
  "libs":    ["MyPackage.lib"],
  "runtime": ["bin/Win32/MyPackage.dll"],
  "defines": ["MYPACKAGE_ENABLED"],
  "csharp":  ["csharp"],
  "samples": ["samples/Example.h", "samples/Example.cpp"],

  "editorFeature": "networking"
}
```

`name` is the only required field, and becomes the folder name, so keep it to
letters, digits, dot, dash and underscore. Every path is relative to the
package folder. Everything except `name` may be left out.

To publish one, put that folder at the root of a GitHub repository. Anyone can
then paste the repository URL into the Plugins tab.

## Packages here

| | |
|---|---|
| [ENetworking](ENetworking/README.md) | TCP and UDP networking, the Networking tab, and a component that syncs transforms across the network |
| [UnityConverter](UnityConverter/README.md) | Imports a Unity project: converts its C# scripts, turns its scenes into Elintria scenes, and brings the assets across. Adds the Unity Importer window |

## Where things live

```
<project>/Plugins/<name>/     an installed package
<project>/Plugins/plugins.json   which ones are switched on
```

`plugins.json` only records the on/off state. Deleting it turns everything
back on; deleting a package folder uninstalls it. Both are safe to do by hand
while the editor is closed.
