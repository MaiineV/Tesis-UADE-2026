# System #0400 — Audio Manager wiring

> Levantar el Audio System (§17.A) y enchufarlo al Feedback pipeline (§10).
> Todo el C# ya está en `Assets/Scripts/Rollgeon/Audio/` — lo que sigue es
> setup autoral en el engine.

---

## §0. Archivos involucrados

Código (ya commiteado):

- `Assets/Scripts/Rollgeon/Audio/IAudioService.cs`
- `Assets/Scripts/Rollgeon/Audio/AudioChannel.cs`
- `Assets/Scripts/Rollgeon/Audio/AudioSettingsSO.cs`
- `Assets/Scripts/Rollgeon/Audio/AudioManager.cs`
- `Assets/Scripts/Rollgeon/Audio/AudioManagerBootstrap.cs`
- Edit en `Assets/Scripts/Rollgeon/Feedback/FeedbackManager.cs` —
  `DispatchSFX` rutea por `IAudioService` con fallback a
  `PlayClipAtPoint` para EditMode tests.

Setup manual (este doc):

1. `Assets/Rollgeon/Audio/RollgeonMixer.mixer`
2. `Assets/Rollgeon/Audio/AudioSettings.asset`
3. `Assets/Rollgeon/Audio/AudioManagerBootstrap.asset`
4. Entrada en `ServiceBootstrap.ExtraServices`.

---

## §1. Crear el AudioMixer

1. `Assets / Create / Audio / Audio Mixer` → nombrarlo **`RollgeonMixer`**
   en `Assets/Rollgeon/Audio/`.
2. Abrir el mixer y en la ventana **Audio Mixer**:
   - El grupo raíz ya es `Master` — dejarlo.
   - Agregar 3 child groups: **`Music`**, **`Sfx`**, **`Ui`**.
3. Exponer un parámetro de volumen por cada grupo:
   - Seleccionar `Master` → click derecho sobre el slider de **Volume**
     en el inspector → **Expose 'Volume (of Master)' to script**.
   - Repetir para `Music`, `Sfx` y `Ui`.
4. En la ventana **Exposed Parameters** (arriba a la derecha del mixer),
   renombrar los parámetros para que el default del `AudioSettingsSO`
   matchee sin editar:
   - `MyExposedParam` → **`MasterVol`**
   - `MyExposedParam 1` → **`MusicVol`**
   - `MyExposedParam 2` → **`SfxVol`**
   - `MyExposedParam 3` → **`UiVol`**
   (si los nombras distinto, overridealos en `AudioSettings.asset` en §2.)

---

## §2. Crear el AudioSettingsSO

1. `Assets / Create / Rollgeon / Audio / Audio Settings` →
   `Assets/Rollgeon/Audio/AudioSettings.asset`.
2. Inspector del asset:
   - **Mixer** → arrastrar `RollgeonMixer`.
   - **MasterParam / MusicParam / SfxParam / UiParam** — dejar los
     defaults si seguiste §1.4.
   - **SfxGroup** → arrastrar el grupo `Sfx` del mixer.
   - **MusicGroup** → arrastrar el grupo `Music`.
   - **UiGroup** → arrastrar el grupo `Ui`.
   - **SfxPoolSize** → `16` está bien para FP.
   - Volúmenes por defecto: dejar los valores sugeridos
     (Master 1.0, Music 0.8, Sfx 1.0, Ui 1.0).
   - **BiomeMusic** — dejar vacío por ahora. Se llena cuando el
     `DungeonManager` enganche `PlayMusicForBiome`.

---

## §3. Crear el AudioManagerBootstrap

1. `Assets / Create / Rollgeon / Audio / Audio Manager Bootstrap` →
   `Assets/Rollgeon/Audio/AudioManagerBootstrap.asset`.
2. Inspector:
   - **Settings** → arrastrar `AudioSettings.asset` de §2.

---

## §4. Engancharlo al ServiceBootstrap

1. Abrir `Assets/Rollgeon/ServiceBootstrap.asset`.
2. En **Extra Services**, agregar `AudioManagerBootstrap.asset`.
3. Orden sugerido: antes del `FeedbackManagerBootstrap`. El priority
   numérico ya fuerza el orden correcto (Audio 50 < Feedback 55), pero
   tenerlos visualmente ordenados ayuda al review.

---

## §5. Verificación

1. Entrar a Play. En la consola debe aparecer el log de bootstrap sin
   errores de `[AudioManagerBootstrap]` ni `[AudioManager]`.
2. En la jerarquía (con `DontDestroyOnLoad`) deben aparecer los GO
   `[AudioManager]`, `[MusicA]`, `[MusicB]` y 16 `[SfxSource_N]`.
3. Disparar cualquier `EffPlayFeedback(hit.basic)` en combate: el SFX
   debe sonar ruteado por el mixer. Bajar el volumen del canal `Sfx`
   en el mixer durante Play para confirmar que respeta el routing.
4. Llamar manualmente desde consola (o un behavior temporal):
   `ServiceLocator.GetService<IAudioService>().PlayMusic(clip, 1f);`
   — la música debería crossfadearse en 1 s.

---

## §6. Lo que queda pendiente (fuera de este ticket)

- **Volúmenes persistentes.** `AudioManager` implementa `ISaveable`
  con key `"audio.volumes"`, pero el SaveSystem (§15) todavía es stub.
  Cuando aterrice el save real, registrar el manager en
  `SaveSystem.Register(audioManager)` al final de `Bootstrap`.
- **Música por biome.** El `DungeonManager` debería llamar
  `IAudioService.PlayMusicForBiome(room.BiomeId)` en `EnterRoom`. No
  está cableado aún — se hace cuando el pipeline de biome ids aterrice.
- **Ducking en pausa.** §D.2 pide duckear música/sfx cuando
  `IScreenManager.OnPauseChanged == true`. Se agrega como handler en
  `AudioManager` cuando el `ScreenManager` real exista.
- **Camera shake vía service.** §E.10 dice que `ICameraService.Shake`
  reemplaza el shake directo de `FeedbackManager`. Vive fuera del
  scope del sistema de audio, queda para cuando se extienda
  `ICameraService`.

---

## §7. Cross-ref

- TECHNICAL.md §10 — pipeline de feedback, `DispatchSFX` ahora rutea acá.
- TECHNICAL.md §17.A — spec del audio system.
- TECHNICAL.md §15 — SaveSystem, para la integración futura de volúmenes.
- `docs/setup/System#0300_FeedbackManagerWiring.md` — hermano, wiring del
  FeedbackManager que este sistema complementa.
