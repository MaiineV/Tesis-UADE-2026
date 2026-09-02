<#
.SYNOPSIS
    Cliente de la API REST de DocsApp (el GDD del proyecto, workspace Rollgeon).

.DESCRIPTION
    Lee el PAT y los ids del workspace desde el .env de la raiz del repo
    (ver docs/setup/docsapp-api.md). El .env NO se commitea.

    Comandos:
      teams                          Equipos y tu rol
      tree                           Arbol de paginas del workspace
      find   <texto>                 Busca paginas por titulo
      get    <docId>                 Baja una pagina (markdown por defecto)
      create -Title <t> -ParentId <id> [-File body.md] [-Icon x]
      update <docId> [-Title t] [-Icon x] [-File body.md]
      delete <docId> -Force          Manda a papelera (recuperable en /docs/trash)

.EXAMPLE
    ./tools/docsapp/docsapp.ps1 tree

.EXAMPLE
    # Editar una seccion: bajar -> tocar el markdown -> subir el doc COMPLETO
    ./tools/docsapp/docsapp.ps1 get de5a27be-4297-4971-872a-be553a9d6904 -Out cofre.md
    ./tools/docsapp/docsapp.ps1 update de5a27be-4297-4971-872a-be553a9d6904 -File cofre.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('teams', 'tree', 'find', 'get', 'create', 'update', 'delete')]
    [string]$Command,

    [Parameter(Position = 1)]
    [string]$Arg,

    [string]$TeamId,
    [string]$ParentId,
    [string]$Title,
    [string]$Icon,
    [ValidateSet('markdown', 'json')]
    [string]$Format = 'markdown',
    [string]$File,
    [string]$Out,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }
# Sin esto la consola de Windows escupe "?" en los titulos con acentos y emojis.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Import-DotEnv {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
        $sep = $trimmed.IndexOf('=')
        if ($sep -lt 1) { continue }
        $key = $trimmed.Substring(0, $sep).Trim()
        $value = $trimmed.Substring($sep + 1).Trim().Trim('"').Trim("'")
        if ($value -ne '') { Set-Item -Path "env:$key" -Value $value }
    }
}

Import-DotEnv (Join-Path $repoRoot '.env')
# Fallback para agents que tienen vedado tocar archivos `.env*` (reglas de permisos de
# Claude Code): mismo formato, carpeta ignorada por git. `.env` gana si define el PAT.
if (-not $env:DOCSAPP_PAT) { Import-DotEnv (Join-Path $repoRoot '.secrets\docsapp.env') }

$baseUrl = if ($env:DOCSAPP_BASE_URL) { $env:DOCSAPP_BASE_URL.TrimEnd('/') }
           else { 'https://docs-app-orcin.vercel.app/api/v1' }
$pat = $env:DOCSAPP_PAT
if (-not $pat -or $pat -like '*pegar_token*') {
    Write-Host "Falta DOCSAPP_PAT." -ForegroundColor Red
    Write-Host "Pega el token en $repoRoot\.env o en $repoRoot\.secrets\docsapp.env (se genera en https://docs-app-orcin.vercel.app/profile/tokens)."
    Write-Host "Template y pasos: docs/setup/docsapp-api.md"
    exit 1
}
if (-not $TeamId) { $TeamId = $env:DOCSAPP_TEAM_ID }

# La API responde JSON UTF-8; Invoke-RestMethod en PS 5.1 rompe los acentos, por eso
# vamos por Invoke-WebRequest y decodificamos los bytes a mano.
function Invoke-Docs {
    param(
        [string]$Method,
        [string]$Path,
        $Body
    )
    $headers = @{ Authorization = "Bearer $pat"; Accept = 'application/json' }
    $req = @{
        Method          = $Method
        Uri             = "$baseUrl$Path"
        Headers         = $headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 30 -Compress
        $req.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $req.ContentType = 'application/json; charset=utf-8'
    }

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-WebRequest @req
            $text = [System.Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray())
            if ([string]::IsNullOrWhiteSpace($text)) { return $null }
            return $text | ConvertFrom-Json
        }
        catch {
            $webResponse = $_.Exception.Response
            $status = if ($webResponse) { [int]$webResponse.StatusCode } else { 0 }

            if ($status -eq 429 -and $attempt -lt 3) {
                $wait = 5
                try { if ($webResponse.Headers['Retry-After']) { $wait = [int]$webResponse.Headers['Retry-After'] } } catch { }
                Write-Warning "429 rate limit (120 req/min). Reintento en $wait s."
                Start-Sleep -Seconds $wait
                continue
            }

            $detail = ''
            if ($webResponse) {
                try {
                    $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
                    $detail = $reader.ReadToEnd()
                }
                catch { }
            }
            if ($status -eq 401) { $detail = "Token invalido o revocado. $detail" }
            if ($status -eq 409) { $detail = "Conflicto de version: alguien guardo el doc mientras editabas. Rehace el GET -> PATCH. $detail" }
            throw "HTTP $status en $Method $Path`n$detail"
        }
    }
}

function Get-Field {
    param($Object, [string[]]$Names)
    foreach ($name in $Names) {
        if ($Object -and $Object.PSObject.Properties[$name]) { return $Object.$name }
    }
    return $null
}

function Get-Documents {
    if (-not $TeamId) { throw 'Falta el team id: pasa -TeamId o defini DOCSAPP_TEAM_ID en el .env.' }
    $result = Invoke-Docs -Method GET -Path "/teams/$TeamId/documents"
    $docs = Get-Field $result @('documents', 'data')
    if ($null -eq $docs) { $docs = $result }
    return @($docs)
}

function Get-DocContent {
    param($Doc)
    $content = Get-Field $Doc @('content', 'body', 'markdown')
    if ($null -eq $content) { $content = Get-Field (Get-Field $Doc @('document')) @('content', 'body') }
    return $content
}

function Write-Tree {
    param($Docs, $ParentValue, [int]$Depth)
    $children = @($Docs | Where-Object {
            $parent = Get-Field $_ @('parent_id', 'parentId')
            ($null -eq $parent -and $null -eq $ParentValue) -or ($parent -eq $ParentValue)
        } | Sort-Object { [int](Get-Field $_ @('position')) })

    foreach ($child in $children) {
        $id = Get-Field $child @('id')
        $title = Get-Field $child @('title')
        $icon = Get-Field $child @('icon')
        $indent = '  ' * $Depth
        $prefix = if ($icon) { "$icon " } else { '' }
        Write-Host ("{0}{1}{2}  " -f $indent, $prefix, $title) -NoNewline
        Write-Host $id -ForegroundColor DarkGray
        Write-Tree -Docs $Docs -ParentValue $id -Depth ($Depth + 1)
    }
}

function Save-Text {
    param([string]$Path, [string]$Text)
    $full = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path (Get-Location).Path $Path }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($full), $Text, $utf8NoBom)
}

switch ($Command) {

    'teams' {
        $result = Invoke-Docs -Method GET -Path '/teams'
        $teams = Get-Field $result @('teams', 'data')
        if ($null -eq $teams) { $teams = $result }
        @($teams) | ForEach-Object {
            [pscustomobject]@{
                Nombre = Get-Field $_ @('name')
                Rol    = Get-Field $_ @('role')
                Id     = Get-Field $_ @('id')
            }
        } | Format-Table -AutoSize
    }

    'tree' {
        $docs = Get-Documents
        Write-Host "$($docs.Count) paginas en el workspace`n"
        Write-Tree -Docs $docs -ParentValue $null -Depth 0
    }

    'find' {
        if (-not $Arg) { throw 'Uso: docsapp.ps1 find <texto>' }
        $docs = Get-Documents
        $hits = @($docs | Where-Object { (Get-Field $_ @('title')) -match [regex]::Escape($Arg) })
        if ($hits.Count -eq 0) { Write-Host "Sin resultados para '$Arg'."; break }
        $hits | ForEach-Object {
            [pscustomobject]@{
                Titulo = Get-Field $_ @('title')
                Id     = Get-Field $_ @('id')
            }
        } | Format-Table -AutoSize
    }

    'get' {
        if (-not $Arg) { throw 'Uso: docsapp.ps1 get <docId> [-Format markdown|json] [-Out archivo]' }
        $doc = Invoke-Docs -Method GET -Path "/documents/$($Arg)?format=$Format"
        if ($Format -eq 'json') {
            $text = $doc | ConvertTo-Json -Depth 40
        }
        else {
            $text = [string](Get-DocContent $doc)
        }
        if ($Out) {
            Save-Text -Path $Out -Text $text
            Write-Host "Guardado en $Out"
        }
        else {
            $text
        }
    }

    'create' {
        if (-not $Title) { throw 'Uso: docsapp.ps1 create -Title "<titulo>" -ParentId <id> [-File body.md] [-Icon x]' }
        if (-not $ParentId) {
            Write-Warning 'Sin -ParentId el doc queda suelto en la raiz del equipo. La convencion del workspace es colgarlo de una seccion.'
        }
        $body = @{ title = $Title }
        if ($ParentId) { $body.parent_id = $ParentId }
        if ($Icon) { $body.icon = $Icon }
        if ($File) {
            $body.content = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $File).Path)
            $body.format = 'markdown'
        }
        $created = Invoke-Docs -Method POST -Path "/teams/$TeamId/documents" -Body $body
        $newId = Get-Field $created @('id')
        if (-not $newId) { $newId = Get-Field (Get-Field $created @('document')) @('id') }
        Write-Host "Creado: $Title"
        Write-Host "  id:  $newId"
        Write-Host "  url: https://docs-app-orcin.vercel.app/docs/$newId"
    }

    'update' {
        if (-not $Arg) { throw 'Uso: docsapp.ps1 update <docId> [-Title t] [-Icon x] [-File body.md]' }
        if (-not $Title -and -not $Icon -and -not $File) { throw 'Nada para actualizar: pasa -Title, -Icon o -File.' }
        $body = @{}
        if ($Title) { $body.title = $Title }
        if ($Icon) { $body.icon = $Icon }
        if ($File) {
            # PATCH pisa el cuerpo entero: el archivo tiene que traer el doc COMPLETO.
            $body.content = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $File).Path)
            $body.format = 'markdown'
        }
        Invoke-Docs -Method PATCH -Path "/documents/$Arg" -Body $body | Out-Null
        Write-Host "Actualizado: https://docs-app-orcin.vercel.app/docs/$Arg"
    }

    'delete' {
        if (-not $Arg) { throw 'Uso: docsapp.ps1 delete <docId> -Force' }
        if (-not $Force) { throw 'delete manda el doc y sus subpaginas a la papelera. Repeti con -Force si es lo que queres.' }
        Invoke-Docs -Method DELETE -Path "/documents/$Arg" | Out-Null
        Write-Host "Enviado a la papelera. Recuperable en https://docs-app-orcin.vercel.app/docs/trash"
    }
}
