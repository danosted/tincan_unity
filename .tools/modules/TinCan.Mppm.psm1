# Multiplayer Play Mode clones (Player 2 and up): finding them and keeping them in step with the main Editor before a
# host + client run.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")
Import-Module (Join-Path $PSScriptRoot "TinCan.Editor.psm1")

# Read once per run (see Get-ClonePaths); the prefab check also runs once per run.
$script:ClonePaths = $null
$script:PrefabsChecked = $false

# MPPM clones can come back from a relaunch with an empty untitled scene. They then "play" nothing and never join,
# so before a host + client run, open the host's active scene in every clone. A clone that already has it open reopens
# it too: clones do not reload a scene that changed on disk (for example after Rebuild Scenes), and would run the stale one.
# The clone list is read once per run: each `unity status` costs about 1.5 s.
function Get-ClonePaths {
    if ($null -eq $script:ClonePaths) {
        $script:ClonePaths = @(& unity status 2>$null | Select-String "Library[\\/]VP[\\/]" | ForEach-Object {
            ($_.ToString() -split "\t|\s{2,}") | Where-Object { $_ -match "Library[\\/]VP[\\/]" } | Select-Object -First 1
        })
    }
    return $script:ClonePaths
}

# No clone running (MPPM does not relaunch Player 2 after an Editor restart): launch it through the harness menu, wait
# for it to show in `unity status` and answer, and return the clone paths. The first launch of a clone takes minutes.
function Start-PlayerTwo([int]$TimeoutSeconds = 300) {
    Write-Tier "Editor" "note" "no Player 2 running; launching it (TinCan > Dev > Net Harness > Launch Player 2)"
    Invoke-Unity @("menu", "--path", "TinCan/Dev/Net Harness/Launch Player 2") 30 | Out-Null

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $script:ClonePaths = $null
        $clones = @(Get-ClonePaths)
        if ($clones.Count -eq 0) { continue }

        $ready = @($clones | Where-Object { $null -ne (Invoke-Unity @("editor_status", "--project-path", $_) 15).playMode })
        if ($ready.Count -eq $clones.Count) {
            Write-Tier "Editor" "note" "Player 2 is up"
            return $clones
        }
    }

    Stop-Unusable "Player 2 did not come up within $TimeoutSeconds s; start it in Window > Multiplayer > Multiplayer Play Mode"
}

function Sync-CloneScenes {
    $main = Invoke-Unity @("list_open_scenes") 20
    $mainScene = @($main.scenes | Where-Object { $_.isActive })[0].path
    if (-not $mainScene) { return }

    $clones = @(Get-ClonePaths)
    if ($clones.Count -eq 0) { $clones = @(Start-PlayerTwo) }

    foreach ($clone in $clones) {
        $name = Split-Path $clone -Leaf
        # Right after a host + client run the clone is still leaving Play mode, and refuses to open a scene until it has.
        $deadline = (Get-Date).AddSeconds(30)
        while ((Invoke-Unity @("editor_status", "--project-path", $clone) 15).playMode -notin @($null, "stopped")) {
            if ((Get-Date) -gt $deadline) { Stop-Unusable "clone $name is still in Play mode after 30 s" }
            Start-Sleep -Milliseconds 500
        }

        $scenes = Invoke-Unity @("list_open_scenes", "--project-path", $clone) 20
        $active = @($scenes.scenes | Where-Object { $_.isActive })[0].path
        Invoke-Unity @("open_scene", "--project-path", $clone, "--path", $mainScene) 60 | Out-Null

        # A client in another scene never joins; the run then only shows "no subject player". Confirm, don't assume.
        $now = @((Invoke-Unity @("list_open_scenes", "--project-path", $clone) 20).scenes | Where-Object { $_.isActive })[0].path
        if ($now -ne $mainScene) { Stop-Unusable "clone $name did not open $mainScene (still has '$now')" }
        if ($active -ne $mainScene) { Write-Tier "Editor" "note" "clone $name had '$active' open; opened $mainScene" }
    }
}

# NGO refuses a client whose network prefab hashes differ from the host's ("NetworkConfig mismatch"), which a scenario
# only shows as "no subject player". A clone reads prefabs from disk while the host may hold a newer in-memory hash
# (for example right after a prefab was created from a scene object), so compare them before a host + client run.
# Prefabs do not change during a run, so a batch checks once.
function Confirm-NetworkPrefabsMatch {
    if ($script:PrefabsChecked) { return }
    $script:PrefabsChecked = $true
    $code = 'var sb = new System.Text.StringBuilder(); foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })) { var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid); var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path); var no = go != null ? go.GetComponent<Unity.Netcode.NetworkObject>() : null; if (no != null) sb.Append(path).Append("=").Append(no.PrefabIdHash).Append(";"); } return sb.ToString();'
    $hostPrefabs = (Invoke-Unity @("eval", "--code", $code) 60).result
    if (-not $hostPrefabs) { return }

    foreach ($clone in Get-ClonePaths) {
        $clonePrefabs = (Invoke-Unity @("eval", "--project-path", $clone, "--code", $code) 60).result
        if (-not $clonePrefabs -or $clonePrefabs -eq $hostPrefabs) { continue }

        $hostSet = $hostPrefabs -split ";" | Where-Object { $_ }
        $cloneSet = $clonePrefabs -split ";" | Where-Object { $_ }
        $diff = Compare-Object $hostSet $cloneSet | ForEach-Object { "$($_.SideIndicator -replace '<=','host' -replace '=>','clone'): $($_.InputObject)" }
        Stop-Unusable ("network prefab hashes differ between host and clone $(Split-Path $clone -Leaf); the client would be refused (NetworkConfig mismatch). " +
            "On the host, EditorUtility.SetDirty(prefab) + AssetDatabase.SaveAssets() writes the in-memory hash to disk:`n  " + ($diff -join "`n  "))
    }
}

Export-ModuleMember -Function Get-ClonePaths, Sync-CloneScenes, Confirm-NetworkPrefabsMatch
