# Build de Windows — cómo generar el player (Feature#0036)

Cómo sacar un `Rollgeon.exe` desde el repo. Para subirlo a Steam después,
seguir [`SteamPipe/README.md`](../../SteamPipe/README.md).

## Rápido

**Rollgeon → Build → Windows 64 (Development)** — o `(Release)`.

Sale en `Build/Windows64/Rollgeon.exe` (gitignoreado). Al terminar abre la carpeta.

Desde CLI, para CI:

```
Unity.exe -quit -batchmode -projectPath <repo> -buildTarget Win64 \
  -executeMethod Rollgeon.EditorTools.RollgeonBuild.BuildWindows64Release \
  -buildPath <dir opcional>
```

En batch mode, una validación fallida o un build fallido salen con código 1.

## Qué valida antes de buildear

`Assets/Scripts/Editor/Build/RollgeonBuild.cs` **valida y aborta, no corrige**.
Corregir en cada build ensuciaría `ProjectSettings.asset` y metería ruido de git en
cada corrida. Los chequeos existen porque todos estos fallan **en silencio** — el
player se genera igual y el problema aparece mucho después:

| Chequeo | Qué rompe si pasa desapercibido |
|---|---|
| `productName == Rollgeon` | Nombre del `.exe` y carpeta de saves (`persistentDataPath`) |
| `companyName == LetItRide` | Idem |
| `applicationIdentifier` sin `Unity-Technologies` | Queda el ID del template URP |
| Define `STEAMWORKS_NET` presente | El player shippea **sin Steam** y compila igual |
| Define `DISABLESTEAMWORKS` ausente | `SteamServiceBootstrap` se compila vacío |
| Build target activo `StandaloneWindows64` | Buildearías otra plataforma |
| Escena 0 == `00_Bootstrap` | El juego arranca **sin servicios registrados** y sin error |

El script no cambia el build target por su cuenta: `SwitchActiveBuildTarget` dispara
un domain reload a mitad del método y el build no llega a ocurrir. Avisa y corta.

## Addressables

`AddressableAssetSettings` tiene **Build Addressables on Player Build =
BuildWithPlayer**, así que el contenido se construye solo con cada player. No hay
paso manual.

Esto es deliberado: el valor anterior (`PreferencesValue`) delegaba en un `EditorPrefs`
**que no vive en el repo**, así que el resultado dependía de la máquina. Con el prefs
apagado se shippeaba un player sin string tables y el menú principal salía en blanco.

Los 5 grupos son locales y no hay remote catalog, así que no hace falta hostear nada.
Para inspeccionar el contenido: **Window → Asset Management → Addressables → Groups**.

## `steam_appid.txt`

El post-build lo copia **siempre** al lado del `.exe`. Sin él, `SteamAPI.Init()` falla
al correr el juego fuera del cliente de Steam (el juego sigue andando, pero sin Steam).

El depot lo **excluye** por vdf. Una sola salida de build, dos consumidores, y la
exclusión vive en el borde que le importa — así no hay dos variantes de build entre
las que elegir mal.

## Verificar una build

```
Build/Windows64/
  Rollgeon.exe                                       ← no "Rolllgeon.exe"
  steam_appid.txt                                    ← 4889850
  Rollgeon_Data/Plugins/x86_64/steam_api64.dll       ← Steamworks
  Rollgeon_Data/StreamingAssets/aa/catalog.bin          ← contenido Addressables
  Rollgeon_Data/StreamingAssets/aa/StandaloneWindows64/
      localization-string-tables-{english,spanish}_assets_all.bundle
```

Con Steam abierto, correr el `.exe` y revisar el log:

```
C:\Users\<user>\AppData\LocalLow\LetItRide\Rollgeon\Player.log
```

Tiene que aparecer `[Steam] Init OK — AppId 4889850, usuario '<nombre>'.`

Si el log se sigue escribiendo en la carpeta vieja `LetItRide\Rolllgeon\`, el cambio
de `productName` no tomó.

**Para validar localización, mirar los botones del menú principal**, no la pantalla de
Opciones: `LocalizedContent` cae a fallbacks en español y se ve perfecta aunque no
haya cargado ninguna tabla. El menú usa `LocalizeStringEvent`, que sale en blanco o
con la key cruda si el contenido falta.

## Cosas que NO son bugs

- **No hay overlay de Steam** al lanzar el `.exe` desde el explorador, aunque
  `Init()` dé OK. El overlay requiere lanzar el juego *a través* del cliente de Steam.
- **Los logros devuelven `false`** hasta publicarlos en el partner site
  (ver [`steamworks-setup.md`](./steamworks-setup.md)).
- **Splash de Unity** — licencia Personal, no se puede sacar.

## Deuda conocida

- Backend **Mono**, no IL2CPP. Está bien para desarrollo. Al migrar hay que correr el
  paso de AOT de Odin, y ojo con `ServiceBootstrap.asset`: si `ExtraServices`
  deserializa vacío, **Steam nunca inicializa y no hay ningún error**.
- El asmdef del DevConsole compila en builds de release. Es inalcanzable (su installer
  está gateado a `UNITY_EDITOR || DEVELOPMENT_BUILD`), pero el código de cheats viaja
  en el assembly. Se arregla con `defineConstraints` en el asmdef.
- ~555 `Debug.Log` sin strippear en código de runtime.
