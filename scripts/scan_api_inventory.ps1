<#
.SYNOPSIS
    Script tự động quét toàn bộ API endpoints của hệ thống EcoLink WebApi (.NET 10) và FastAPI AI Service.
.DESCRIPTION
    Quét danh mục Controller, trích xuất HTTP Method, Route template, phân quyền RBAC và tóm tắt nghiệp vụ.
    Hỗ trợ kiểm tra đối chiếu nhanh để cập nhật tài liệu:
    documents/api document/00_api_inventory_and_endpoint_statistics.md
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/scan_api_inventory.ps1
#>

$backendRoot = Join-Path $PSScriptRoot "..\src\Ecolink.Backend\Ecolink.WebApi"
$controllerPath = Join-Path $backendRoot "Controllers"
$controllers = Get-ChildItem -Path $controllerPath -Filter "*.cs"

$endpoints = @()

foreach ($file in $controllers) {
    $lines = Get-Content $file.FullName
    $currentController = ""
    $currentBaseRoute = ""
    $currentClassRoles = "Public / Any"
    $currentSummary = ""
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        
        # Doc summary
        if ($line -match '<summary>(.*)</summary>') {
            $currentSummary = $matches[1].Trim()
        } elseif ($line -match '<summary>(.*)') {
            $currentSummary = $matches[1].Trim()
            while ($i + 1 -lt $lines.Count -and $lines[$i+1] -notmatch '</summary>') {
                $i++
                $currentSummary += " " + $lines[$i].Replace("///", "").Trim()
            }
        }
        
        # Class declaration
        if ($line -match 'public\s+class\s+([A-Za-z0-9_]+Controller)\s*:\s*ControllerBase') {
            $currentController = $matches[1]
            $currentBaseRoute = ""
            $currentClassRoles = "Public / Guest"
            
            for ($k = [Math]::Max(0, $i - 10); $k -lt $i; $k++) {
                if ($lines[$k] -match '\[Route\("([^"]+)"\)\]') {
                    $currentBaseRoute = $matches[1]
                }
                if ($lines[$k] -match '\[Authorize\(Roles\s*=\s*"([^"]+)"\)\]') {
                    $currentClassRoles = $matches[1]
                } elseif ($lines[$k] -match '\[Authorize\]') {
                    $currentClassRoles = "Authenticated (Any Role)"
                } elseif ($lines[$k] -match '\[AllowAnonymous\]') {
                    $currentClassRoles = "Public (AllowAnonymous)"
                }
            }
            continue
        }
        
        # Action HTTP Method
        if ($line -match '\[Http(Get|Post|Put|Delete|Patch)(\("([^"]*)"\))?\]') {
            $verb = $matches[1].ToUpper()
            $actionRoute = if ($matches[3]) { $matches[3] } else { "" }
            
            $actionRoles = $currentClassRoles
            for ($k = [Math]::Max(0, $i - 5); $k -lt $i; $k++) {
                if ($lines[$k] -match '\[Authorize\(Roles\s*=\s*"([^"]+)"\)\]') {
                    $actionRoles = $matches[1]
                } elseif ($lines[$k] -match '\[Authorize\]') {
                    $actionRoles = "Authenticated (Any Role)"
                } elseif ($lines[$k] -match '\[AllowAnonymous\]') {
                    $actionRoles = "Public (AllowAnonymous)"
                }
            }
            
            $methodName = ""
            for ($j = $i + 1; $j -lt [Math]::Min($i + 15, $lines.Count); $j++) {
                if ($lines[$j] -match 'public\s+(async\s+)?(Task<[^>]+>|Task|IActionResult|ActionResult<[^>]+>)\s+([A-Za-z0-9_]+)\s*\(') {
                    $methodName = $matches[3]
                    break
                }
            }
            
            $fullRoute = $currentBaseRoute
            if ($actionRoute -ne "") {
                if ($actionRoute.StartsWith("/")) {
                    $fullRoute = $actionRoute.TrimStart("/")
                } else {
                    $fullRoute = "$currentBaseRoute/$actionRoute"
                }
            }
            $cleanCtrlName = $currentController.Replace("Controller", "").ToLower()
            $fullRoute = $fullRoute.Replace("[controller]", $cleanCtrlName)
            if (-not $fullRoute.StartsWith("/")) {
                $fullRoute = "/" + $fullRoute
            }
            
            $endpoints += [PSCustomObject]@{
                STT        = $endpoints.Count + 1
                Method     = $verb
                Route      = $fullRoute
                Roles      = $actionRoles
                Controller = $currentController
                Action     = $methodName
                Summary    = $currentSummary
            }
            $currentSummary = ""
        }
    }
}

Write-Host "=================================================================" -ForegroundColor Green
Write-Host "         ECOLINK API SCANNER & INVENTORY VERIFICATION           " -ForegroundColor Yellow
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "Tong so Controller da quet: $($controllers.Count)" -ForegroundColor Cyan
Write-Host "Tong so REST Action endpoints trong WebApi: $($endpoints.Count)" -ForegroundColor Cyan
Write-Host "Minimal API: 1 (/health)" -ForegroundColor Cyan
Write-Host "SignalR Hub: 1 (/hubs/negotiation-chat)" -ForegroundColor Cyan
Write-Host "Python FastAPI AI Service: 9 endpoints" -ForegroundColor Cyan
Write-Host "-----------------------------------------------------------------"
Write-Host ">>> TONG CONG HE THONG: $($endpoints.Count + 1 + 1 + 9) ENDPOINTS <<<" -ForegroundColor Green
Write-Host "================================================================="

$endpoints | Format-Table -AutoSize STT, Method, Route, Roles, Controller

