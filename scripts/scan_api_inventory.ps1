<#
.SYNOPSIS
    Automated scanner for all EcoLink API endpoints across .NET 10 WebApi and Python FastAPI AI Service.
.DESCRIPTION
    Scans all Controller .cs files, Program.cs (Minimal API & SignalR Hubs), and src/Ecolink.AiService/app.py.
    Groups [Http*] attributes per C# Action Method so both Canonical Actions (199) and Total Route Bindings
    including backward-compatibility aliases (233) are tracked accurately.
    Syncs with: documents/api contact/00_api_inventory_and_endpoint_statistics.md
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/scan_api_inventory.ps1
#>

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $repoRoot "src\Ecolink.Backend\Ecolink.WebApi"
$controllerPath = Join-Path $backendRoot "Controllers"
$staffPermFile = Join-Path $repoRoot "src\Ecolink.Backend\Ecolink.Domain\Constants\StaffPermissions.cs"
$programFile = Join-Path $backendRoot "Program.cs"
$aiAppFile = Join-Path $repoRoot "src\Ecolink.AiService\app.py"

# Build permission constant -> code map from StaffPermissions.cs
$permMap = @{}
if (Test-Path $staffPermFile) {
    $permLines = Get-Content -Path $staffPermFile -Encoding UTF8
    foreach ($pl in $permLines) {
        if ($pl -match 'public\s+const\s+string\s+([A-Za-z0-9_]+)\s*=\s*"([^"]+)"') {
            $permMap["StaffPermissions.$($matches[1])"] = $matches[2]
        }
    }
}

function Resolve-AuthLabel {
    param(
        [string]$RawAttr,
        [string]$Fallback
    )
    if ($RawAttr -match '^\[Authorize\(Roles\s*=\s*"([^"]+)"\)\]') {
        return $matches[1]
    } elseif ($RawAttr -match '^\[Authorize\]') {
        return "Authenticated (Any Role)"
    } elseif ($RawAttr -match '^\[AllowAnonymous\]') {
        return "Public (AllowAnonymous)"
    } elseif ($RawAttr -match '^\[HasPermission\(([^\)]+)\)\]') {
        $arg = $matches[1].Trim()
        $resolved = if ($permMap.ContainsKey($arg)) { $permMap[$arg] } else { $arg.Trim('"') }
        return "Staff [$resolved] / Admin"
    }
    return $Fallback
}

function Build-FullRoute {
    param(
        [string]$BaseRoute,
        [string]$ActionRoute,
        [string]$ControllerName
    )
    $full = $BaseRoute
    if (-not [string]::IsNullOrEmpty($ActionRoute)) {
        if ($ActionRoute.StartsWith("api/") -or $ActionRoute.StartsWith("/")) {
            $full = $ActionRoute.TrimStart("/")
        } elseif ([string]::IsNullOrEmpty($BaseRoute)) {
            $full = $ActionRoute
        } else {
            $full = "$BaseRoute/$ActionRoute"
        }
    }
    $cleanCtrl = $ControllerName.Replace("Controller", "").ToLowerInvariant()
    $full = $full.Replace("[controller]", $cleanCtrl)
    $full = "/" + ($full.TrimStart("/") -replace '/+', '/')
    return $full
}

$controllerFiles = Get-ChildItem -Path $controllerPath -Filter "*.cs" | Sort-Object Name
$controllerClasses = [System.Collections.Generic.HashSet[string]]::new()
$canonicalEndpoints = @()
$totalControllerBindings = 0
$totalControllerAliases = 0

foreach ($file in $controllerFiles) {
    $lines = Get-Content -Path $file.FullName -Encoding UTF8
    $currentController = ""
    $currentBaseRoute = ""
    $currentClassRoles = "Public / Guest (No Attribute)"
    $currentSummary = ""
    $pendingAttrs = @()

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()

        # XML doc summary
        if ($line -match '^///\s*<summary>(.*)</summary>') {
            $currentSummary = ($matches[1] -replace '<[^>]+>', '').Trim()
            continue
        } elseif ($line -match '^///\s*<summary>(.*)') {
            $buf = $matches[1].Trim()
            while ($i + 1 -lt $lines.Count -and $lines[$i + 1] -notmatch '</summary>') {
                $i++
                $buf += " " + $lines[$i].Replace("///", "").Trim()
            }
            if ($i + 1 -lt $lines.Count) { $i++ }
            $currentSummary = ($buf -replace '</?summary>', '' -replace '<[^>]+>', '').Trim()
            continue
        }

        # Collect attributes (supports multi-line attribute syntax)
        if ($line -match '^\[') {
            $attrBuf = $line
            while (($attrBuf.ToCharArray() | Where-Object { $_ -eq '[' }).Count -gt ($attrBuf.ToCharArray() | Where-Object { $_ -eq ']' }).Count -and ($i + 1) -lt $lines.Count) {
                $i++
                $attrBuf += " " + $lines[$i].Trim()
            }
            $pendingAttrs += $attrBuf
            continue
        }

        # Controller class declaration
        if ($line -match '^public\s+class\s+([A-Za-z0-9_]+Controller)\s*:\s*ControllerBase') {
            $currentController = $matches[1]
            [void]$controllerClasses.Add($currentController)
            $currentBaseRoute = ""
            $currentClassRoles = "Public / Guest (No Attribute)"

            foreach ($attr in $pendingAttrs) {
                if ($attr -match '^\[Route\("([^"]+)"\)\]') {
                    $currentBaseRoute = $matches[1]
                } else {
                    $currentClassRoles = Resolve-AuthLabel -RawAttr $attr -Fallback $currentClassRoles
                }
            }
            $pendingAttrs = @()
            $currentSummary = ""
            continue
        }

        # Public action method declaration (handles nested generic return types)
        if ($line -match '^public\s+(?:async\s+)?(?:virtual\s+|override\s+)?[\w<>\.\?\[\],\s]+\s+([A-Za-z0-9_]+)\s*\(') {
            $methodName = $matches[1]
            $httpAttrs = @($pendingAttrs | Where-Object { $_ -match '^\[Http(Get|Post|Put|Patch|Delete)' })

            if ($httpAttrs.Count -gt 0) {
                $actionRoles = $currentClassRoles
                foreach ($attr in $pendingAttrs) {
                    $actionRoles = Resolve-AuthLabel -RawAttr $attr -Fallback $actionRoles
                }

                $parsedRoutes = @()
                foreach ($ha in $httpAttrs) {
                    if ($ha -match '^\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]') {
                        $v = $matches[1].ToUpperInvariant()
                        $r = if ($matches[2]) { $matches[2] } else { "" }
                        $full = Build-FullRoute -BaseRoute $currentBaseRoute -ActionRoute $r -ControllerName $currentController
                        $parsedRoutes += [PSCustomObject]@{ Method = $v; Route = $full }
                    }
                }

                if ($parsedRoutes.Count -gt 0) {
                    $primary = $parsedRoutes[0]
                    $aliases = @()
                    for ($k = 1; $k -lt $parsedRoutes.Count; $k++) {
                        $aliases += "$($parsedRoutes[$k].Method) $($parsedRoutes[$k].Route)"
                    }

                    $totalControllerBindings += $parsedRoutes.Count
                    $totalControllerAliases += ($parsedRoutes.Count - 1)

                    $canonicalEndpoints += [PSCustomObject]@{
                        STT        = $canonicalEndpoints.Count + 1
                        Method     = $primary.Method
                        Route      = $primary.Route
                        Roles      = $actionRoles
                        Controller = $currentController
                        Action     = $methodName
                        Bindings   = $parsedRoutes.Count
                        Aliases    = ($aliases -join "; ")
                        Summary    = $currentSummary
                    }
                }
            }
            $pendingAttrs = @()
            $currentSummary = ""
            continue
        }

        if ($line -ne "" -and $line -notmatch '^///' -and $line -notmatch '^//') {
            $pendingAttrs = @()
            $currentSummary = ""
        }
    }
}

# Scan Program.cs for Minimal APIs and SignalR Hubs
$minimalApis = @()
$signalRHubs = @()
if (Test-Path $programFile) {
    $progContent = Get-Content -Path $programFile -Raw -Encoding UTF8
    $minMatches = [regex]::Matches($progContent, 'app\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"')
    foreach ($m in $minMatches) {
        $minimalApis += [PSCustomObject]@{
            Method  = $m.Groups[1].Value.ToUpperInvariant()
            Route   = $m.Groups[2].Value
            Hub     = "-"
            Source  = "Program.cs (Minimal API)"
        }
    }
    $hubMatches = [regex]::Matches($progContent, 'app\.MapHub<([^>]+)>\("([^"]+)"\)')
    foreach ($m in $hubMatches) {
        $signalRHubs += [PSCustomObject]@{
            Method  = "WS"
            Route   = $m.Groups[2].Value
            Hub     = $m.Groups[1].Value
            Source  = "Program.cs (SignalR Hub)"
        }
    }
}

# Scan Python FastAPI app.py dynamically
$fastApiEndpoints = @()
if (Test-Path $aiAppFile) {
    $aiLines = Get-Content -Path $aiAppFile -Encoding UTF8
    for ($i = 0; $i -lt $aiLines.Count; $i++) {
        $l = $aiLines[$i].Trim()
        if ($l -match '^@app\.(get|post|put|patch|delete)\("([^"]+)"(.*)') {
            $verb = $matches[1].ToUpperInvariant()
            $route = $matches[2]
            $tags = ""
            if ($l -match 'tags=\["([^"]+)"\]') {
                $tags = $matches[1]
            }
            $handler = ""
            for ($j = $i + 1; $j -lt [Math]::Min($i + 8, $aiLines.Count); $j++) {
                if ($aiLines[$j].Trim() -match '^(?:async\s+)?def\s+([A-Za-z0-9_]+)\s*\(') {
                    $handler = $matches[1]
                    break
                }
            }
            $fastApiEndpoints += [PSCustomObject]@{
                STT     = $fastApiEndpoints.Count + 1
                Method  = $verb
                Route   = $route
                Handler = $handler
                Tags    = $tags
            }
        }
    }
}

$coreCanonicalCount = $canonicalEndpoints.Count + $minimalApis.Count + $signalRHubs.Count
$coreTotalBindings = $totalControllerBindings + $minimalApis.Count + $signalRHubs.Count
$fastApiCount = $fastApiEndpoints.Count
$grandTotalCanonical = $coreCanonicalCount + $fastApiCount
$grandTotalBindings = $coreTotalBindings + $fastApiCount

# Compute HTTP verb breakdown across all 218 canonical endpoints
$allCanonicalForStats = @()
$allCanonicalForStats += $canonicalEndpoints | Select-Object Method
$allCanonicalForStats += $minimalApis | Select-Object Method
$allCanonicalForStats += $signalRHubs | Select-Object Method
$allCanonicalForStats += $fastApiEndpoints | Select-Object Method
$verbGroups = $allCanonicalForStats | Group-Object Method

Write-Host "================================================================================" -ForegroundColor Green
Write-Host "              ECOLINK API SCANNER & CANONICAL INVENTORY VERIFIER                " -ForegroundColor Yellow
Write-Host "================================================================================" -ForegroundColor Green
Write-Host ("Controller .cs files scanned        : {0} ({1} Controller classes)" -f $controllerFiles.Count, $controllerClasses.Count) -ForegroundColor Cyan
Write-Host ("Canonical Controller REST Actions   : {0}" -f $canonicalEndpoints.Count) -ForegroundColor Cyan
Write-Host ("Total Controller Bindings (incl Ali): {0} ({1} canonical + {2} aliases)" -f $totalControllerBindings, $canonicalEndpoints.Count, $totalControllerAliases) -ForegroundColor Cyan
Write-Host ("Minimal APIs (Program.cs)           : {0} ({1})" -f $minimalApis.Count, (($minimalApis | ForEach-Object { "$($_.Method) $($_.Route)" }) -join ", ")) -ForegroundColor Cyan
Write-Host ("SignalR WebSocket Hubs (Program.cs) : {0} ({1})" -f $signalRHubs.Count, (($signalRHubs | ForEach-Object { $_.Route }) -join ", ")) -ForegroundColor Cyan
Write-Host ("Core .NET 10 (Ecolink.WebApi)       : {0} Canonical Endpoints ({1} Bindings)" -f $coreCanonicalCount, $coreTotalBindings) -ForegroundColor Cyan
Write-Host ("Dedicated AI Service (FastAPI)      : {0} REST Endpoints" -f $fastApiCount) -ForegroundColor Cyan
Write-Host "--------------------------------------------------------------------------------"
Write-Host (">>> GRAND TOTAL: {0} Canonical Endpoints ({1} Core .NET + {2} FastAPI) / {3} Route Bindings (incl. {4} aliases) <<<" -f $grandTotalCanonical, $coreCanonicalCount, $fastApiCount, $grandTotalBindings, $totalControllerAliases) -ForegroundColor Green
Write-Host "--------------------------------------------------------------------------------"
Write-Host "HTTP Method Breakdown (Canonical):" -ForegroundColor Yellow
foreach ($v in @("GET", "POST", "PUT", "PATCH", "DELETE", "WS")) {
    $cnt = ($verbGroups | Where-Object { $_.Name -eq $v } | Select-Object -ExpandProperty Count)
    if (-not $cnt) { $cnt = 0 }
    Write-Host ("  {0,-6}: {1}" -f $v, $cnt)
}
Write-Host "================================================================================"

Write-Host "`n[1] Core .NET 10 Controller Canonical Actions ($($canonicalEndpoints.Count)):" -ForegroundColor Yellow
$canonicalEndpoints | Format-Table -AutoSize STT, Method, Route, Roles, Controller, Action, Aliases

Write-Host "`n[2] Minimal API ($($minimalApis.Count)) & SignalR Hubs ($($signalRHubs.Count)):" -ForegroundColor Yellow
@($minimalApis) + @($signalRHubs) | Format-Table -AutoSize Method, Route, Hub, Source

Write-Host "`n[3] Python FastAPI AI Service Endpoints ($fastApiCount):" -ForegroundColor Yellow
$fastApiEndpoints | Format-Table -AutoSize STT, Method, Route, Handler, Tags
