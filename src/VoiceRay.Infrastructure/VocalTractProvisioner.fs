namespace VoiceRay.Infrastructure

open System
open System.IO

/// Ensures sagittal reference art is present under client/public for the Vite app.
module VocalTractProvisioner =
    let private assetName = "reference.png"
    let private publicName = "vocal-tract-reference.png"
    let private svgName = "vocal-tract.svg"

    let private repoRoot (contentRoot: string) =
        let candidate = Path.GetFullPath(Path.Combine(contentRoot, "..", ".."))
        if Directory.Exists(Path.Combine(candidate, "client")) then candidate else contentRoot

    let isReady (contentRoot: string) =
        let root = repoRoot contentRoot
        let publicDir = Path.Combine(root, "client", "public")
        File.Exists(Path.Combine(publicDir, publicName))
        && File.Exists(Path.Combine(publicDir, svgName))

    let tryProvision (contentRoot: string) =
        if isReady contentRoot then
            Ok()
        else
            try
                let root = repoRoot contentRoot
                let assetsDir = Path.Combine(root, "assets", "vocal-tract")
                let publicDir = Path.Combine(root, "client", "public")
                Directory.CreateDirectory publicDir |> ignore

                let sourceAsset = Path.Combine(assetsDir, assetName)
                let destPng = Path.Combine(publicDir, publicName)

                if File.Exists sourceAsset then
                    ProvisionLog.info "Copying vocal tract reference image to client/public…"
                    File.Copy(sourceAsset, destPng, true)

                if not (File.Exists destPng) then
                    Error "Vocal tract reference image is missing from assets and public folders."
                elif not (File.Exists(Path.Combine(publicDir, svgName))) then
                    Error "vocal-tract.svg is missing from client/public."
                else
                    ProvisionLog.info "Sagittal vocal tract assets are ready."
                    Ok()
            with ex ->
                Error ex.Message
