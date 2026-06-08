namespace VoiceRay.Infrastructure

open System

type ResourceSnapshot =
    { Id: string
      Label: string
      Status: string
      Detail: string
      Required: bool
      CanAutoProvision: bool }

module SetupProvisioner =
    let private resourceSnapshots (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        [ { Id = "piper"
            Label = "Piper TTS (reference audio)"
            Status =
                if PiperProvisioner.isReady piper then
                    "ready"
                else
                    "missing"
            Detail = PiperProvisioner.statusMessage piper
            Required = true
            CanAutoProvision = OperatingSystem.IsWindows() }
          { Id = "whisper"
            Label = "Whisper alignment cache"
            Status =
                if WhisperProvisioner.isReady alignment then
                    "ready"
                else
                    "missing"
            Detail = WhisperProvisioner.resolveCacheDir alignment
            Required = false
            CanAutoProvision = true }
          { Id = "vocalTract"
            Label = "Sagittal diagram assets"
            Status =
                if VocalTractProvisioner.isReady contentRoot then
                    "ready"
                else
                    "missing"
            Detail = "reference.png + vocal-tract.svg"
            Required = true
            CanAutoProvision = true }
          { Id = "mfa"
            Label = "MFA Docker worker (optional)"
            Status =
                match alignment.MfaWorkerUrl with
                | Some url when not (System.String.IsNullOrWhiteSpace url) -> "configured"
                | _ -> "optional"
            Detail = "In-process mfa-stub is used when the worker is not running."
            Required = false
            CanAutoProvision = false } ]

    let allRequiredReady (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        resourceSnapshots contentRoot piper alignment
        |> List.filter (fun r -> r.Required)
        |> List.forall (fun r -> r.Status = "ready")

    let buildStatus (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        let snap = ProvisionLog.snapshot ()
        {| state = snap.state
           lastError = snap.lastError
           logs = snap.logs
           resources = resourceSnapshots contentRoot piper alignment
           ready = allRequiredReady contentRoot piper alignment |}

    let private runCore (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        ProvisionLog.info "VoiceRay setup started."

        if not (VocalTractProvisioner.isReady contentRoot) then
            ProvisionLog.info "Setting up sagittal diagram assets…"

            match VocalTractProvisioner.tryProvision contentRoot with
            | Error message -> failwith message
            | Ok () -> ()

        if not (WhisperProvisioner.isReady alignment) then
            ProvisionLog.info "Setting up Whisper alignment cache…"

            match WhisperProvisioner.tryProvision alignment with
            | Error message -> failwith message
            | Ok () -> ()

        if not (PiperProvisioner.isReady piper) then
            if not (OperatingSystem.IsWindows()) then
                failwith "Piper TTS auto-setup requires Windows."

            ProvisionLog.info "Setting up Piper TTS (download may take several minutes)…"

            match PiperProvisioner.tryProvision piper with
            | Error message -> failwith message
            | Ok () -> ProvisionLog.info "Piper TTS is ready."

        if allRequiredReady contentRoot piper alignment then
            ProvisionLog.info "All required resources are ready."
        else
            failwith "Some required resources are still missing."

    let runSync (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        if ProvisionLog.isRunning () then
            Error "Setup is already running."
        elif not (ProvisionLog.tryBeginRun ()) then
            Error "Setup could not start."
        else
            try
                runCore contentRoot piper alignment
                ProvisionLog.endRun true None
                Ok()
            with ex ->
                ProvisionLog.error ex.Message
                ProvisionLog.endRun false (Some ex.Message)
                Error ex.Message

    let startBackground (contentRoot: string) (piper: PiperOptions) (alignment: AlignmentOptions) =
        if ProvisionLog.isRunning () then
            false
        elif not (ProvisionLog.tryBeginRun ()) then
            false
        else
            System.Threading.Tasks.Task.Run(fun () ->
                try
                    runCore contentRoot piper alignment
                    ProvisionLog.endRun true None
                with ex ->
                    ProvisionLog.error ex.Message
                    ProvisionLog.endRun false (Some ex.Message))
            |> ignore

            true
