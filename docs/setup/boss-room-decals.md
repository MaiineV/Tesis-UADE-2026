# Decals temáticos de las salas de jefe

> Estado al 2026-08-13. Los materiales ya se generan por código; el wiring de
> los `DecalProjector` en las salas queda a mano (no lo hace ningún tool).

## 1. Generar los materiales

`Tools → Rollgeon → Bosses → Import Casino Sprites` (una vez) y después
`Tools → Rollgeon → Bosses → Build Casino Decal Materials`.

Salen cuatro `.mat` en `Assets/Art/2D/Symbols/`, clonados de
`DecalHerradura.mat` (shader `Assets/Shaders/DecalSymbols.shadergraph`,
propiedades `Base_Map` + `_Color`):

| Material           | Símbolo       | Color     |
|--------------------|---------------|-----------|
| `Decal_Ruleta.mat` | `Casino_0048` | borravino |
| `Decal_Fichas.mat` | `Casino_0038` | dorado    |
| `Decal_Dados.mat`  | `Casino_0044` | azul navy |
| `Decal_Cartas.mat` | `Casino_0054` | violeta   |

Re-correr el menú es seguro: repopula los `.mat` existentes (conserva su GUID,
no rompe salas ya cableadas) y no marca nada sucio si ya coinciden. Para
retunear un color, editar `CasinoDecalMaterialBuilder.Specs` y re-correr.

## 2. Qué material va en qué sala

Los decals viven en el prefab de la **sala**, no en el del jefe: todos los
jefes de un piso comparten sala, así que el símbolo identifica al **piso**, no
al jefe individual (un swap por jefe necesitaría un componente de runtime —
fuera de alcance).

| Sala                                                     | Piso | Jefes del pool                          | Material                       |
|----------------------------------------------------------|------|-----------------------------------------|--------------------------------|
| `Assets/Prefabs/Rooms/FloorOne/Boss_Room01.prefab`       | 1    | Sunken Grand · Croupier · Bandida       | `Decal_Ruleta`                 |
| `Assets/Prefabs/Rooms/FloorTwo/Boss_Room_FloorTwo01.prefab` | 2 | Cajero · Anotador (Security Boss off)   | `Decal_Fichas`                 |
| `Assets/Prefabs/Rooms/FloorThree/Boss_Room_FloorThree.prefab` | 3 | Generala · Tahúr (General Director off) | `Decal_Dados` + `Decal_Cartas` |

El pool por piso lo define `Tools → Rollgeon → Bosses → Build Floor Pools`
(ver `BossPoolAssetInstaller`); si cambian los pools, revisar esta tabla.

Piso 3 lleva los dos: la mesa de la Generala son dados y el Tahúr son cartas.
Alternar proyectores en el mismo grupo (mitad y mitad) lee mejor que un solo
símbolo repetido.

## 3. Cómo cablearlos

En cada sala, dentro del grupo **`Decals`**:

1. **No toques** los `RedDot*` / `BlueDot*` existentes. Usan
   `Assets/Art/3D/Materials/Decal/Decal_Ficha.mat` y `Decal_Ficha 1.mat`
   (círculos rojo/azul) y son marcadores de spawn, no decoración.
2. Duplicá uno de esos proyectores (hereda rotación `X 90 / Z -90`, `Size`
   1×1×1, `Offset Z 0.5`, `Fade Factor 0.5`) y renombralo, ej.
   `Symbol_Ruleta`.
3. En el `DecalProjector`, cambiá **`Material`** al `.mat` del piso.
4. Posicionalo sobre el piso donde se para el jefe. Escala sugerida de
   arranque: la del `DecalHerradura` en escena (0.88 uniforme).
5. Repetí 2-4 tantas veces como haga falta. Piso 3: alterná
   `Decal_Dados` / `Decal_Cartas`.

## Limitaciones conocidas

- El tinte se aplica en `_Color` del material, así que **todos los proyectores
  que compartan el `.mat` comparten el color**. Para dos tonos del mismo
  símbolo hacen falta dos materiales.
- El `_Color` clonado hereda `alpha = 0` de `DecalHerradura` (el shadergraph
  no lee esa alpha; la opacidad real la maneja `Fade Factor` del proyector).
  Si algún día el shader empieza a leerla, revisar
  `CasinoDecalMaterialBuilder.ApplySpec`.
