$moduleName = "Microsoft.Graph.Authentication"

# Use Find-Module to check if it exists in the gallery
$installed = Get-Module -ListAvailable | Where-Object { $_.Name -eq $moduleName }

if (-not $installed) {
    Write-Output "[INFO] '$moduleName' not found. Attempting to install..."
    try {
        Install-Module -Name $moduleName -Scope CurrentUser -Force -ErrorAction Stop
        Write-Output "[INFO] Module '$moduleName' installed successfully."
    }
    catch {
        Write-Output "[ERROR] Failed to install '$moduleName': $($_.Exception.Message)"
        exit 1
    }
} else {
    Write-Output "[INFO] '$moduleName' is already installed."
}

# Attempt to import
try {
    Import-Module $moduleName -ErrorAction Stop
    Write-Output "[INFO] Successfully imported '$moduleName'."
}
catch {
    Write-Output "[ERROR] Failed to import module '$moduleName': $($_.Exception.Message)"
    exit 1
}

trap [System.Management.Automation.PipelineStoppedException] {
    Write-Output "`n[!] Script canceled by user via Ctrl+C. Exiting now...`n"
    Disconnect-MgGraph -ErrorAction SilentlyContinue
    exit 1
}

function Show-DepartmentPrompt {
    param($departmentCodes)
    $deptPrompt = "Please select from the following department options: " +
        (($departmentCodes.GetEnumerator() | Sort-Object { [int]$_.Name } | ForEach-Object { "[$($_.Key)] $($_.Value)" }) -join ", ")
    do {
        $deptSelection = Read-Host -Prompt ("$deptPrompt (or type 'c' to restart)")
        if ($deptSelection -eq "c") { throw [System.Exception]::new("userCancel") }
        if (-not $departmentCodes.ContainsKey($deptSelection)) {
            Write-Output "`n[!] Invalid department selection. Please enter a valid number or 'c' to restart.`n"
            Start-Sleep -Seconds 1
        }
    } while (-not $departmentCodes.ContainsKey($deptSelection))
    return $deptSelection
}

function Show-GroupTypePrompt {
    do {
        $groupTypeSelection = Read-Host -Prompt "Please select the group type by number: [1] Root Folder [2] Subfolder [3] User Group [4] Department ReadOnly Group (type 'c' to restart)"
        if ($groupTypeSelection -eq "c") { throw [System.Exception]::new("userCancel") }
        if ($groupTypeSelection -notin @("1", "2", "3", "4")) {
            Write-Output "`n[!] Invalid selection. Please enter 1, 2, 3, or 4 (or 'c').`n"
            Start-Sleep -Seconds 1
        }
    } while ($groupTypeSelection -notin @("1", "2", "3", "4"))
    return $groupTypeSelection
}

function Prompt-Input ($promptText) {
    $input = Read-Host -Prompt "$promptText (type 'c' to restart)"
    if ($input -eq "c") { throw [System.Exception]::new("userCancel") }
    return $input
}

try {
    Connect-MgGraph -Scopes "Group.ReadWrite.All", "AdministrativeUnit.ReadWrite.All" -NoWelcome -ErrorAction Stop
    $departmentCodes = @{
    "1"  = "ACCT"
    "2"  = "FINC"
    "3"  = "MGMT"
    "4"  = "BizGrad"
    "5"  = "UAO"
    "6"  = "INFO"
    "7"  = "DEAN"
    "8"  = "BUSP"
    "9"  = "COMM"
    "10" = "CIBS"
    "11" = "CED"
    "12" = "UAVS"
    "13" = "MKTG"
}
    $auId = "d4d0b7e6-233c-41c0-939a-e271578427ca"
    $logFile = Join-Path $PSScriptRoot "group-creation.log"

    while ($true) {
        Clear-Host
        try {
            # --- DEPARTMENT PROMPT ---
            $deptSelection = Show-DepartmentPrompt $departmentCodes
            $deptCode = $departmentCodes[$deptSelection]

            # --- GROUP TYPE PROMPT ---
            $groupTypeSelection = Show-GroupTypePrompt

            # --- GROUP NAME LOGIC ---
            switch ($groupTypeSelection) {
                '1' {
                    do {
                        $folderName = Prompt-Input "Please enter the folder name"
                        if ([string]::IsNullOrWhiteSpace($folderName)) {
                            Write-Output "`n[!] Folder name cannot be empty. Please try again.`n"
                        }
                    } while ([string]::IsNullOrWhiteSpace($folderName))
                    $groupName = "FSG-CLBA-$deptCode-$folderName"
                }

             '2' {
    # Subfolder logic
    $rootPrefix = "FSG-CLBA-$deptCode-"
    $rootGroups = @()
    $skipToken = $null
    do {
        $filter = "startswith(displayName,'$rootPrefix')"
        $uri = "https://graph.microsoft.com/beta/groups?`$filter=$filter&`$top=50&`$select=displayName"
        if ($skipToken) { $uri += "&`$skiptoken=$skipToken" }
        $response = Invoke-MgGraphRequest -Method GET -Uri $uri -ErrorAction Stop
        $rootGroups += $response.value
        $skipToken = if ($response.'@odata.nextLink') {
            ($response.'@odata.nextLink' -split 'skiptoken=')[1]
        } else { $null }
    } while ($skipToken)

    $rootFolderNames = $rootGroups | ForEach-Object {
        if ($_.displayName.StartsWith($rootPrefix)) {
            $_.displayName.Substring($rootPrefix.Length)
        }
    } | Sort-Object -Unique

    if (-not $rootFolderNames -or $rootFolderNames.Count -eq 0) {
        Write-Output "`n[!] No root folders available. Create a root folder first.`n"
        Start-Sleep 2
        continue
    }

    Write-Output "`nAvailable root folders:`n------------------------"
    $rootFolderNames | ForEach-Object { Write-Output " - $_" }
    Write-Output "------------------------`n"

    do {
        $chosenRoot = Prompt-Input "Enter the root folder name"
        $normalizedInput = $chosenRoot.Trim().ToLower()

        $matchedRoot = $rootFolderNames | Where-Object {
            $_.Trim().ToLower() -eq $normalizedInput
        }

        if (-not $matchedRoot) {
            Write-Output "`n[!] Invalid root folder. Please enter a valid name exactly as shown above.`n"
        }
    } while (-not $matchedRoot)

    $chosenRoot = $matchedRoot

    # ⚠️ Subfolder nesting warning
    Write-Output "`n⚠️ WARNING: Deep nesting of subfolder-based groups can lead to confusion and complexity in access control."
    Write-Output "Use nested subfolder levels **only when necessary**. Default is 1 level."

    # Ask how many subfolder levels
    do {
        $levelInput = Read-Host -Prompt "How many subfolder levels do you want to create? (1–3 recommended, or type 'c' to restart)"
        if ($levelInput -eq "c") { throw [System.Exception]::new("userCancel") }
        $isNumeric = [int]::TryParse($levelInput, [ref]$null)
    } while (-not $isNumeric -or [int]$levelInput -lt 1)

    $subFolderParts = @()
    for ($i = 1; $i -le [int]$levelInput; $i++) {
        do {
            $subPart = Prompt-Input "Enter name for subfolder level $i"
            if ([string]::IsNullOrWhiteSpace($subPart)) {
                Write-Output "`n[!] Subfolder name cannot be empty. Please try again.`n"
            }
        } while ([string]::IsNullOrWhiteSpace($subPart))
        $subFolderParts += $subPart
    }

    $fullSubPath = ($subFolderParts -join "-")
    $groupName = "CSG-CLBA-$deptCode-$chosenRoot-$fullSubPath"
}


                '3' {
                    do {
                        $groupPart = Prompt-Input "Enter the user group name (after 'Mays-Group-')"
                        if ([string]::IsNullOrWhiteSpace($groupPart)) {
                            Write-Output "`n[!] Group name cannot be empty. Please try again.`n"
                        }
                    } while ([string]::IsNullOrWhiteSpace($groupPart))
                    $groupName = "CSG-CLBA-$deptCode-Mays-Group-$groupPart"
                }

                '4' {
                    $groupName = "CSG-CLBA-$deptCode-Mays-Group-All Department(ReadOnly SharePoint Site (Limited))"
                }

                default {
                    throw "Invalid group type selection"
                }
            }

            # --- MAILNICKNAME LOGIC ---
            $mailNicknameRaw = $groupName -replace "[^a-zA-Z0-9]", ""
            $mailNickname = $mailNicknameRaw.Substring(0, [Math]::Min(64, $mailNicknameRaw.Length))
            $GroupJSON = @"
{
    "displayName": "${groupName}",
    "mailEnabled": false,
    "securityEnabled": true,
    "mailNickname": "${mailNickname}",
    "description": "Created via MgGraph API call.",
    "@odata.type": "#microsoft.graph.group"
}
"@

            # --- CREATE GROUP ---
            $createdGroup = Invoke-MgGraphRequest -Method POST `
                -Uri "https://graph.microsoft.com/beta/directory/administrativeUnits/$auId/members" `
                -Body $GroupJSON -ErrorAction Stop

            # --- LOG TO FILE ON SUCCESS ---
            $logEntry = "$(Get-Date -Format s) - Group '$groupName' created. ID: $($createdGroup.id)"
            Add-Content -Path $logFile -Value $logEntry

            Write-Output "`n========== SUCCESS =========="
            Write-Output "Group '$groupName' created inside Administrative Unit"
            Write-Output "Microsoft Entra ID (Azure AD) Group ID: $($createdGroup.id)"
            Write-Output "This result is auditable in $logFile"
            Write-Output "=============================`n"

            $continue = Read-Host -Prompt "Do you want to create another group? [y/n] (or 'c' to restart)"
            if ($continue -eq "c") { continue }
            elseif ($continue -match '^[nN]$') { break }

        } catch {
            if ($_.Exception.Message -eq 'userCancel') {
                continue
            }
            Write-Output "`n========== ERROR =========="
            Write-Output "Error: $($_.Exception.Message)"
            Write-Output "==========================="

            $continue = Read-Host -Prompt "Do you want to try again? [y/n] (or 'c' to restart)"
            if ($continue -eq "c") { continue }
            elseif ($continue -match '^[nN]$') { break }
        }
    }
}
catch {
    Write-Output "`nFATAL ERROR: $($_.Exception.Message)`n"
}
finally {
    Disconnect-MgGraph -ErrorAction SilentlyContinue
    Write-Output "Exiting script. Goodbye!"
}