# DocsApp API — conectar los agents al GDD

El GDD del proyecto vive en **DocsApp** (workspace *Rollgeon*). La app expone una
API REST, así que Claude (o cualquier agent) puede leer specs y editarlas sin que
las copiemos a mano al repo.

Referencia completa de la API: `docs/API.md` en el repo `MaiineV/DocsApp`.
Página equivalente en el workspace: *How-To → Usar la API de DocsApp con Claude*.

---

## 1. Generar el token (PAT)

1. Entrar a <https://docs-app-orcin.vercel.app/profile/tokens>.
2. Crear un **Personal Access Token**:
   - **Scope**: `read` si los agents solo leen; `read_write` si además crean o
     editan páginas.
   - **Vencimiento**: opcional (30/90 días o sin vencimiento).
3. Copiarlo en el momento — **se muestra una sola vez** (arranca con `dapp_…`).

El PAT actúa **como tu usuario**: el agent tiene exactamente tus permisos en cada
equipo (owner > admin > editor > viewer). Se revoca al instante desde esa misma
página.

## 2. Guardarlo en `.env`

El archivo `.env` de la raíz del repo está gitignoreado (`/.env`, `/.env.*`).
Crearlo con este contenido y pegar el token:

```dotenv
# DocsApp — API REST del GDD. NUNCA commitear este archivo.
DOCSAPP_PAT=dapp_pegar_token_aca

# Ids del workspace Rollgeon (no son secretos)
DOCSAPP_BASE_URL=https://docs-app-orcin.vercel.app/api/v1
DOCSAPP_TEAM_ID=c7d3ba54-476a-4c8c-bb05-a7176c9b59f6
DOCSAPP_ROOT_DOC_ID=4ce29f83-4cd0-4a64-95ae-4791e558c5b8
DOCSAPP_HOWTO_DOC_ID=dcefb40b-aff4-4de5-aedf-090af986a6c5
```

> Si el token se filtra: revocarlo en `/profile/tokens` y generar otro. No va en
> `CLAUDE.md`, ni en scripts, ni en un doc del workspace.

**Alternativa para agents:** las reglas de permisos de Claude Code (`.claude/settings.json`)
prohíben leer o escribir cualquier `.env*`, así que un agent no puede crear ese archivo. El
mismo contenido puede vivir en `.secrets/docsapp.env` (carpeta gitignoreada); `docsapp.ps1`
lo carga como fallback cuando `.env` no define `DOCSAPP_PAT`.

## 3. Usarlo

Hay un cliente en `tools/docsapp/docsapp.ps1` que levanta el `.env` solo:

```powershell
./tools/docsapp/docsapp.ps1 teams                     # equipos y tu rol (verifica el token)
./tools/docsapp/docsapp.ps1 tree                      # árbol de páginas con sus ids
./tools/docsapp/docsapp.ps1 find "Cofre"              # buscar por título
./tools/docsapp/docsapp.ps1 get <docId>               # imprimir el markdown
./tools/docsapp/docsapp.ps1 get <docId> -Out cofre.md # bajarlo a un archivo

# Editar: bajar -> tocar el markdown -> subir el doc COMPLETO
./tools/docsapp/docsapp.ps1 update <docId> -File cofre.md

# Página nueva colgada de una sección existente
./tools/docsapp/docsapp.ps1 create -Title "Correr los tests de Unity" `
    -ParentId dcefb40b-aff4-4de5-aedf-090af986a6c5 -Icon 🧪 -File nueva.md

./tools/docsapp/docsapp.ps1 delete <docId> -Force      # a la papelera (recuperable)
```

Con `curl` directo es lo mismo:

```bash
curl -H "Authorization: Bearer $DOCSAPP_PAT" \
  "https://docs-app-orcin.vercel.app/api/v1/documents/<doc-id>?format=markdown"
```

### Endpoints

| Método | Ruta | Qué hace |
|--------|------|----------|
| GET | `/teams` | Tus equipos + rol |
| GET | `/teams/{teamId}/documents` | Lista plana (árbol vía `parent_id`, orden por `position`) |
| POST | `/teams/{teamId}/documents` | Crear: `{title?, icon?, parent_id?, content?, format?}` |
| GET | `/documents/{id}?format=markdown` | Leer doc + cuerpo (`json` = bloques lossless) |
| PATCH | `/documents/{id}` | Editar título/ícono/cuerpo — **reemplaza el cuerpo entero** |
| DELETE | `/documents/{id}` | A la papelera (recuperable en `/docs/trash`) |

## Gotchas

- **PATCH pisa el cuerpo completo**, no hay append. El flujo siempre es
  `GET → modificar el markdown → PATCH con todo el doc`. Los editores abiertos
  ven el cambio en vivo.
- **Rate limit**: 120 req/min por usuario (headers `X-RateLimit-*`; 429 con
  `Retry-After`). El script reintenta hasta 3 veces.
- **409 = conflicto de versión**: alguien guardó mientras editabas. Rehacer el
  `GET → PATCH`.
- **Markdown es lossy** para @menciones (quedan como texto). Si el doc las usa y
  hay que preservarlas, trabajar con `format=json`.
- **DELETE es soft**: manda a papelera en cascada con las subpáginas.

## Convenciones del workspace

- Las páginas nuevas cuelgan del doc raíz (README) o de la sección que
  corresponda — no crear docs sueltos en la raíz del equipo.
- Marcar el estado de cada página/sección: ✅ Aprobado por "EL LIDER" /
  🔄 En revisión / Draft / ❌ Pendiente.
- El **índice** de la página *How-To* se actualiza **a mano** al agregar hijas —
  hacerlo en el mismo paso que se crea la página.
