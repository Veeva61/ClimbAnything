# ClimbAnything

Climb **any** surface in **BRUTALISTICK VR**: walls, ledges, pillars, ceilings.

## How it works
Press **grip** while your hand touches a surface. If there is nothing grabbable there, your hand locks onto the surface and the game switches that hand into its own **climbing mode** (native HexaBody climbing strength), so you pull yourself up like on a ladder. Release grip to let go. Normal grabbing of items is not affected.

## Configuration (`UserData/MelonPreferences.cfg` -> `[ClimbAnything]`)
| Setting | Default | Description |
| --- | --- | --- |
| Enabled | true | Master switch |
| GrabRadius | 0.12 | How close (m) the hand must be to a surface |
| AllowDynamicObjects | false | Also hold onto loose physics objects |
| MinDynamicMass | 40 | Minimum mass (kg) of loose objects you can climb |
| BreakForce | 0 | Force (N) at which the grip slips, 0 = never |
| Verbose | true | Log every grab |

Works together with GiantPlayer. Don't use it together with WallClimb.

---

# ClimbAnything (RU)

Лазание по **любой** поверхности: стены, уступы, колонны, потолки.

Нажми **grip**, когда рука касается поверхности. Если рядом нечего взять, рука цепляется за поверхность, а игра включает родной режим лазания HexaBody. Отпусти grip, чтобы отцепиться. Обычный хват предметов не меняется.

## Building

Requires the .NET 6 SDK and BRUTALISTICK VR with MelonLoader 0.7.3 installed through r2modman
(the game must have been started once so MelonLoader generates the Il2Cpp assemblies).

    dotnet build -c Release -p:GameProfile="<path to your r2modman profile>"

The default profile is `%APPDATA%\r2modmanPlus-local\BrutalistickVr\profiles\Default`.
The built dll is copied into the profile's `Mods` folder.
