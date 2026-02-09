# AtomicFramework
## About
AtomicFramework is a framework and utility library for developing mods for the game Nuclear Option.

The goal is to make common modding patterns easier to implement, and to improve interoperability
and user experiences with minimal effort from mod developers.

The end result is ideally a larger modding community with unique ideas, more accessibility
particularly in multiplayer, and higher quality gameplay modding experiences.

## Players and Users
### Installing
Currently, installing AtomicFramework is the same as any other BepInEx mod.

AtomicFramework currently supports BepInEx 5 and 6, but only one at a time.
The correct file must be installed depending on the BepInEx version.

Once BepInEx has been installed, copy the BepInEx folder 
within the AtomicFramework archive over top the existing BepInEx folder.

### Features
Currently, no user facing features are available, though many are planned such as mod toggling.

## Developers
### Installing
As of the writing of this document, no NuGet packages are available.
Otherwise, AtomicFramework can be added as any other library dependency.

All AtomicFramework mods are declared by extending the [`Mod`](./Mod.cs) class.
Once the class is extended, API instances tailored specifically to your mod will be made available.

Of note is that the `Mod` class automatically carries a dependency on AtomicFramework affecting load order.
AtomicFramework also includes a dependency on the NuclearOption process, implicitly carried down via `Mod`.

### Features
AtomicFramework currently support easy Steam based networking ensuring your mod does not conflict
with other compatible mods.

AtomicFramework also supports multiplayer requirement checking, ensuring the host and/or clients
have your mod loaded and enabled if required.

AtomicFramework includes the somewhat popular LoadingManager.
LoadingManager makes easily available several common game events such as when the game finished
loading, a mission starts, and more.

Many additional features are planned, including configuration settings injected into the game's
existing menus.
