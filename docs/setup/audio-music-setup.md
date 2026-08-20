# Setup — Música por contexto + tab de Audio en Opciones

> Feature#0050. La mayor parte del wiring ya está commiteado como YAML
> (MusicLibrary.asset con los 28 clips, MusicDirectorBootstrap.asset, import
> settings de los .wav en Streaming). Quedan **tres pasos de editor** que no se
> pueden hacer desde afuera.

## Qué hace el sistema

- `MusicDirector` (global, creado por `MusicDirectorBootstrap`) escucha los
  eventos del juego y llama `IAudioService.PlayMusic`:
  - `01_MainMenu` cargada → main theme.
  - `OnRoomEntered` / `OnFloorChanged` → exploración (variante del piso).
  - `OnCombatTriggered` → combate; `RoomType.Boss` → track de boss (Phase 1;
    las fases 2/3 quedan importadas para cuando se haga el cambio por fase).
  - `OnCombatEnd` con Victory/Aborted → vuelve a exploración.
- `OptionsScreen` ganó tabs **General | Audio**: sliders Master/Música/Efectos
  + botón de mute por canal (el de Efectos gobierna `Sfx` y `Ui`). Los
  volúmenes/mutes persisten vía `SaveSystem` (`audio.volumes`).

## Pasos de editor (en orden)

1. **Registrar el bootstrap.** Abrir `Assets/Rollgeon/ServiceBootstrap.asset`
   y agregar `Assets/Rollgeon/Services/MusicDirectorBootstrap.asset` a la lista
   **Extra Services** (el orden en la lista no importa: Priority 60 lo ordena
   después de AudioManagerBootstrap=50).

2. **Regenerar el panel de opciones.** Correr los dos installers:
   - `Rollgeon → Juicy Menu → 4 - Setup Options Panel` (escena `01_MainMenu`)
   - `Rollgeon → Juicy Menu → 5 - Setup Pause Options Panel` (`Canvas_PauseMenu.prefab`)

   Crean el header de tabs, los containers `GeneralTab`/`AudioTab`, los tres
   sliders y los mutes, y cablean todo en `OptionsScreen` (idempotentes, además
   agregan las keys de localización nuevas).

3. **Correr los tests EditMode** (Test Runner o MCP `run_tests`): suites nuevas
   `Rollgeon.Audio.Tests` + los casos de audio en `OptionsScreenTests`.

## Checklist de playtest

1. Menú principal → suena el main theme.
2. Run nueva → al entrar a la primera sala, crossfade a exploración piso 1.
3. Cruzar salas → la pista NO se reinicia.
4. Entrar a combate → crossfade rápido a combate; ganar → vuelve exploración.
5. Boss → track del boss del piso.
6. Subir de piso → variante nueva de exploración.
7. Opciones (menú y pausa) → tab Audio: mover sliders con música sonando
   (efecto inmediato), mutear/desmutear cada canal.
8. Cerrar y reabrir el juego → volúmenes y mutes conservados.

## Notas

- `Combat/Floor1` trae `Midnight Casino Duel.ogg` y `Midnight Casino Duel (1).ogg`
  — parecen el mismo track bajado dos veces. Si es así, borrar el `(1)` y
  sacarlo de `MusicLibrary.asset`.
- Los audios se convirtieron de WAV (~875 MB) a **OGG q6 (~99 MB, ninguno >5 MB)**
  con ffmpeg, renombrando los `.meta` para conservar los GUIDs — las referencias
  de `MusicLibrary.asset` siguen válidas. Los WAV originales no se versionaron.
