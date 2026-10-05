# Unity Converter

Brings a Unity project into Elintria: the scripts, the scenes, and the assets.

Install it from **Window ▸ Plugin Manager**, then open **Window ▸ Unity
Importer**, point it at a Unity project folder, and press Import.

---

## What you get

| Unity | What happens |
|---|---|
| `.cs` scripts | Converted and compiled into your project's script assembly |
| `.unity` scenes | Converted to `.elscene`, with objects, transforms, lights, cameras and scripts |
| `.prefab` | Expanded in place wherever a scene uses one |
| Textures, models, audio, materials | Copied into `Assets/`, keeping their folder layout |
| Everything else the project ships | Copied too, unless you turn it off |

Afterwards, read `UnityImport/report.txt` in your project. **That report is the
point.** A conversion between two engines is never complete, and the report is
where every gap is written down, with file names and line numbers.

---

## How scripts are converted

The result is an ordinary Elintria script, not a Unity script running in a
costume. Most of what a converted file needs — transforms, physics, input,
components, coroutines — is the engine's own API, reached directly. The
compatibility layer this package ships covers only the parts of `UnityEngine`
that Elintria has no equivalent for, so a converted script can still say
`using UnityEngine;` and still read like the script you wrote.

What the converter changes:

**The base class becomes `ElintriaScript`.** `MonoBehaviour` is Unity's script
base; `ElintriaScript` is Elintria's, and it has the same lifecycle. Every
mention of `MonoBehaviour` in your code moves with it, so a converted file has
none left — a field declared as one, a `GetComponent<MonoBehaviour>`, a
`typeof`. Mentions inside string literals are left alone and reported, because
a string can be a type name looked up at run time and rewriting one would
change what the script does.

**Nothing is renamed.** `Start` is still `Start`. They do gain `public
override`, because Unity writes these methods private, and a private method in
a subclass hides the base's virtual instead of overriding it — which compiles,
runs, and never gets called:

```csharp
void Start()                      →  public override void Start()
void OnTriggerEnter(Collider c)   →  public override void OnTriggerEnter(Collider c)
```

`Awake`, `OnEnable`, `OnDisable`, `LateUpdate`, `FixedUpdate`, `OnDestroy` and
the six physics callbacks are all corrected the same way.

**Editor-only code is removed.** `#if UNITY_EDITOR` blocks and
`using UnityEditor;` refer to an assembly that does not exist outside Unity.

**Everything else is reported, not changed.**

### What actually works

Real behaviour, not a stand-in:

- `Vector2` / `Vector3` / `Vector4` / `Quaternion` / `Mathf` / `Color` /
  `Matrix4x4` / `Bounds` / `Ray` / `Plane` / `Rect` / `Random` — the same
  arithmetic Unity does, so results match
- `transform.position`, `rotation`, `eulerAngles`, `localScale`, `forward`,
  `Rotate`, `LookAt`, `TransformPoint` and the rest
- **Coroutines** — `StartCoroutine`, `WaitForSeconds`, `WaitUntil`,
  `WaitWhile`, nested routines, `StopCoroutine`
- `Invoke`, `InvokeRepeating`, `CancelInvoke`
- `GetComponent<T>` / `AddComponent<T>` / `SendMessage`, for converted
  scripts **and** for the engine's own components — `Rigidbody`,
  `BoxCollider`, `SphereCollider`, `Light`
- **Physics** — rigid bodies are simulated: `AddForce`, `AddTorque`,
  `velocity`, `angularVelocity`, `mass`, `drag`, gravity, `isKinematic` and
  the freeze constraints. `Physics.Raycast`, `SphereCast`, `Linecast`,
  `OverlapSphere`, `OverlapBox`, `CheckSphere` and `CheckBox` all query the
  same colliders the solver simulates
- **Collision and trigger callbacks** — `OnTriggerEnter`, `OnTriggerStay`,
  `OnTriggerExit`, `OnCollisionEnter`, `OnCollisionStay`, `OnCollisionExit`,
  raised by the solver each fixed step. A collider marked `isTrigger` reports
  through the `OnTrigger` pair and is not pushed apart
- **Creating objects at run time** — `Instantiate`, `new GameObject(...)` and
  `GameObject.CreatePrimitive` all make real objects that appear in the scene
- **The hierarchy is live** — `transform.parent`, `SetParent`, `GetChild`,
  `childCount`, `Find`, `DetachChildren`
- **Tags** — `tag`, `CompareTag`, `FindWithTag`, `FindGameObjectsWithTag`
- **Mouse input** — `GetMouseButton`, `GetMouseButtonDown`, `GetMouseButtonUp`,
  `mousePosition`, `mouseScrollDelta`
- **Audio** — `AudioSource`, `PlayOneShot`, volume, pitch, looping, 3D
  settings. Elintria has a real audio system behind this
- `Input.GetKey` / `GetKeyDown` / `GetKeyUp`, and `GetAxis("Horizontal")` and
  `("Vertical")`
- `Time.deltaTime`, `Time.time`, `Time.fixedDeltaTime`
- `Debug.Log` / `LogWarning` / `LogError`
- `gameObject.SetActive`, `activeSelf`, `GameObject.Find`
- `AnimationCurve` — keys are stored and evaluated properly
- `Texture2D` and `Mesh` — the **data** is real. A script that decodes an
  image or builds geometry produces the right bytes and the right vertices

### What compiles but does nothing

Each of these logs a warning the first time it is called, naming itself, so a
converted project explains its own behaviour instead of just misbehaving:

- **`CharacterController.Move`** — the object moves, nothing stops it. A
  `Rigidbody` with a collider does collide, so that is the way to move a
  character that has to respect the world
- **UI** — `Text`, `Image`, `Canvas`, `Button`. Elintria has its own UI and
  the interface has to be rebuilt
- **Rendering from scripts** — `Renderer.material`, `MeshFilter.mesh`,
  `Shader.Find`. Assign materials in the Elintria editor, or build one with
  its shader graph
- **`Camera.main`** — a detached stand-in; the engine camera is not scriptable
- **`Resources.Load`** — there is no Resources folder; the importer copied
  those files into `Assets/`
- **`SceneManager.LoadScene`**, `Time.timeScale`, `Animator`, `Animation`
- **Tags set in the Unity editor** — the tag API works, but the values did not
  come across; every imported object arrives as `Untagged`

Two things about physics are close rather than exact, and are reported as
notes rather than warnings:

- **A box collider is axis-aligned.** A rotated box is simulated and queried as
  its bounding box, so `Physics.OverlapBox` ignores the orientation argument
- **A collision reports one contact point**, not the several a larger engine
  would give you along a resting face

### What is skipped entirely

A script that cannot compile is left out rather than written broken, because
every script builds into one assembly and one bad file takes all of them down.
The original is kept in `Scripts/_UnityUnconverted/` as a `.txt`, and the
report says why.

- anything in an **`Editor`** folder, or using `UnityEditor`
- **TextMeshPro**, the **Input System** package, **Unity.Mathematics**,
  **Collections**, **Jobs**, **Burst**
- **`UnityEngine.AI`** — Elintria has its own navigation. Use the **Nav Mesh
  Agent** component and `ElintriaNav` instead
- **`ScriptableObject`** assets

If a skipped script declared a type that a converted one uses, the report
lists it under *"Will not compile until these are dealt with"*, grouped by the
script that would fix it.

---

## How scenes are converted

Objects, transforms, names, active state, lights, cameras and scripts all come
across. Script field values come with them, so an object arrives configured
the way it was.

Two things to know:

**The hierarchy is flattened on import.** Elintria's scene file has no place
to record a parent, so every child's transform is composed up to the root and
written in world space. Objects end up exactly where they were.

This is a limit of the scene *format*, not of the engine: the run-time
hierarchy is live, and a script can read and change `transform.parent`,
`SetParent`, `GetChild` and `childCount` normally. What you lose is the
parenting that existed in the Unity scene — if a script relies on it, it has
to rebuild it in `Awake`.

**Unity's built-in primitives are recognised.** A Unity cube becomes an
Elintria Cube rather than an object with a missing mesh. Cube, sphere, capsule,
cylinder, plane and quad are all mapped.

Meshes from your own `.fbx` files are referenced by path. Elintria loads
`.obj`, so an `.fbx` needs converting before it will draw — the report says
which objects are affected.

Materials are **reported, not applied**: the report lists which object had
which material and texture, so you can rebuild them with Elintria's shader
graph without having to go back to Unity to find out what went where.

---

## Where things land

```
<project>/Scripts/*.cs                    converted scripts
<project>/Scripts/_UnityUnconverted/      the ones that were skipped, as .txt
<project>/Scenes/*.elscene                converted scenes
<project>/Assets/**                       textures, models, audio, materials
<project>/UnityImport/report.txt          what happened to everything
```

Scripts are flattened into one folder, because that is where Elintria looks
for them and it requires a script's file name to match its class name. Two
Unity scripts with the same class name in different folders therefore collide;
the importer reports that rather than letting one overwrite the other.

**Overwrite** is off by default. A second import will not undo an afternoon's
editing — it skips what is already there and says so.

---

## Driving it from the AI bridge

- `unity.scan` — report what is in a Unity project, without importing
- `unity.import` — run an import
- `unity.report` — read the last report
- `unity.convertscript` — convert one script, or a snippet, and see the result

---

## Notes

The compatibility layer is compiled into your project's script assembly, so
there is no reference to add and nothing to ship separately. It does not appear
in Add Component: `MonoBehaviour` is abstract, and the rest are not scripts.

Installing this plugin, like any other, copies files and reads a manifest.
Nothing in the package runs at install time.
