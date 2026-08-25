// Mappings of UI optimization IDs to real PowerShell commands
const baseRealCommands: Record<string, {
  commands: { description: string; script: string }[];
  rebootRequired: boolean;
}> = {
  // SYSTEM
  'disable-telemetry': {
    commands: [
      {
        description: "Disabling Telemetry and DiagTrack",
        script: `
          Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue;
          Stop-Service -Name 'DiagTrack' -Force -ErrorAction SilentlyContinue;
          Set-Service -Name 'DiagTrack' -StartupType Disabled -ErrorAction SilentlyContinue;
          Stop-Service -Name 'dmwappushservice' -Force -ErrorAction SilentlyContinue;
          Set-Service -Name 'dmwappushservice' -StartupType Disabled -ErrorAction SilentlyContinue;
          Write-Output 'Telemetry disabled'
        `
      }
    ],
    rebootRequired: true
  },
  'disable-cortana': {
    commands: [
      {
        description: "Disabling Cortana via Group Policy",
        script: `
          $path = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search';
          if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null };
          Set-ItemProperty -Path $path -Name 'AllowCortana' -Value 0 -Type DWord -Force;
          Write-Output 'Cortana disabled'
        `
      }
    ],
    rebootRequired: true
  },
  'disable-search-indexing': {
    commands: [
      {
        description: "Disabling Windows Search Indexing",
        script: `
          Stop-Service -Name 'WSearch' -Force -ErrorAction SilentlyContinue;
          Set-Service -Name 'WSearch' -StartupType Disabled -ErrorAction SilentlyContinue;
          Write-Output 'Search Indexing disabled'
        `
      }
    ],
    rebootRequired: false
  },
  'disable-superfetch': {
    commands: [
      {
        description: "Disabling Superfetch",
        script: `
          Stop-Service -Name 'SysMain' -Force -ErrorAction SilentlyContinue;
          Set-Service -Name 'SysMain' -StartupType Disabled -ErrorAction SilentlyContinue;
          Write-Output 'Superfetch disabled'
        `
      }
    ],
    rebootRequired: true
  },
  'disable-print-spooler': {
    commands: [
      {
        description: "Disabling Print Spooler",
        script: `
          Stop-Service -Name 'Spooler' -Force -ErrorAction SilentlyContinue;
          Set-Service -Name 'Spooler' -StartupType Disabled -ErrorAction SilentlyContinue;
          Write-Output 'Print Spooler disabled'
        `
      }
    ],
    rebootRequired: false
  },
  'disable-xbox-gamebar': {
    commands: [
      {
        description: "Disabling Xbox Game Bar",
        script: `
          foreach ($keyPath in @('HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR', 'HKCU:\\System\\GameConfigStore')) {
            if (-not (Test-Path $keyPath)) { New-Item -Path $keyPath -Force | Out-Null }
          }
          Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR' -Name 'AppCaptureEnabled' -Value 0 -Type DWord -Force;
          Set-ItemProperty -Path 'HKCU:\\System\\GameConfigStore' -Name 'GameDVR_Enabled' -Value 0 -Type DWord -Force;
          Write-Output 'Xbox Game Bar disabled'
        `
      }
    ],
    rebootRequired: true
  },
  'optimize-startup': {
    commands: [
      {
        description: "Optimizing startup items",
        script: `
          Start-Process taskmgr
          Write-Output 'Administrador de Tareas abierto para gestionar el inicio'
        `
      }
    ],
    rebootRequired: false
  },
  'clear-temp-files': {
    commands: [
      {
        description: "Clearing temp folders",
        script: `
          Get-ChildItem -LiteralPath $env:TEMP -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
          Get-ChildItem -LiteralPath "$env:SystemRoot\\Temp" -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
          Get-ChildItem -LiteralPath "$env:SystemRoot\\Prefetch" -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
          Clear-RecycleBin -Force -ErrorAction SilentlyContinue
          Write-Output 'Archivos temporales y papelera limpiados mediante CMD'
        `
      }
    ],
    rebootRequired: false
  },

  // NETWORK
  'dns-optimization': {
    commands: [
      {
        description: "Setting optimal DNS (Cloudflare)",
        script: `
          $interfaces = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
          foreach ($iface in $interfaces) {
            Set-DnsClientServerAddress -InterfaceIndex $iface.ifIndex -ServerAddresses ("1.1.1.1","1.0.0.1") -ErrorAction SilentlyContinue
          }
          Write-Output 'DNS set to Cloudflare (1.1.1.1) for active adapters'
        `
      }
    ],
    rebootRequired: false
  },
  'winsock-reset': {
    commands: [
      {
        description: "Resetting Winsock Catalog",
        script: `
          netsh winsock reset;
          Write-Output 'Winsock catalog reset'
        `
      }
    ],
    rebootRequired: true
  },
  'flush-dns': {
    commands: [
      {
        description: "Flushing DNS Cache",
        script: `
          ipconfig /flushdns;
          Write-Output 'DNS Cache flushed'
        `
      }
    ],
    rebootRequired: false
  },
  'reset-network': {
    commands: [
      {
        description: "Resetting Network Stack",
        script: `
          netsh int ip reset;
          netsh int tcp reset;
          Write-Output 'Network stack reset'
        `
      }
    ],
    rebootRequired: true
  },

  // INPUT
  'mouse-acceleration': {
    commands: [
      {
        description: "Disabling Mouse Acceleration (Enhance Pointer Precision)",
        script: `
          Set-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name 'MouseSpeed' -Value 0 -Force;
          Set-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name 'MouseThreshold1' -Value 0 -Force;
          Set-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name 'MouseThreshold2' -Value 0 -Force;
          Write-Output 'Mouse acceleration disabled'
        `
      }
    ],
    rebootRequired: true
  },
  'keyboard-rate': {
    commands: [
      {
        description: "Optimizing Keyboard Rate",
        script: `
          Set-ItemProperty -Path 'HKCU:\\Control Panel\\Keyboard' -Name 'KeyboardDelay' -Value 0 -Force;
          Set-ItemProperty -Path 'HKCU:\\Control Panel\\Keyboard' -Name 'KeyboardSpeed' -Value 31 -Force;
          Write-Output 'Keyboard responsiveness optimized'
        `
      }
    ],
    rebootRequired: true
  },
  'touchpad-latency': {
    commands: [
      {
        description: "Reducing Touchpad Latency",
        script: `
          $touchpadPath = 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad';
          if (-not (Test-Path $touchpadPath)) { New-Item -Path $touchpadPath -Force | Out-Null }
          Set-ItemProperty -Path $touchpadPath -Name 'AAPThreshold' -Value 0 -Type DWord -Force;
          Write-Output 'Touchpad latency reduced'
        `
      }
    ],
    rebootRequired: false
  },
  'mouse-polling': {
    commands: [
      {
        description: "Mouse Polling Info",
        script: `Write-Output 'Hardware polling rate must be configured via device software (Logitech G HUB, Razer Synapse, etc)'`
      }
    ],
    rebootRequired: false
  },

  // TWEAKS
  'animations': {
    commands: [
      {
        description: "Disabling UI Animations",
        script: `
          Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop\\WindowMetrics' -Name 'MinAnimate' -Value '0' -Force -ErrorAction SilentlyContinue;
          Write-Output 'Window animations disabled'
        `
      }
    ],
    rebootRequired: false
  },
  'transparency': {
    commands: [
      {
        description: "Disabling Transparency Effects",
        script: `
          Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'EnableTransparency' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue;
          Write-Output 'Transparency disabled'
        `
      }
    ],
    rebootRequired: false
  },
  'shadows': {
    commands: [
      {
        description: "Disabling Window Shadows",
        script: `
          Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name 'VisualFXSetting' -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue;
          Write-Output 'Visual effects optimized (Best Performance)'
        `
      }
    ],
    rebootRequired: false
  },
  'taskbar-icons': {
    commands: [
      {
        description: "Aligning Taskbar to Left (Win11)",
        script: `
          Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarAl' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue;
          Write-Output 'Taskbar aligned to left'
        `
      }
    ],
    rebootRequired: false
  },
  'notifications': {
    commands: [
      {
        description: "Disabling Toast Notifications",
        script: `
          $path = 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer';
          if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null };
          Set-ItemProperty -Path $path -Name 'DisableNotificationCenter' -Value 1 -Type DWord -Force;
          Write-Output 'Notifications disabled'
        `
      }
    ],
    rebootRequired: true
  },

  // POWERFUL
  'power-plan': {
    commands: [
      {
        description: "Enabling Ultimate/High Performance Power Plan",
        script: `
          $output = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61;
          if ($LASTEXITCODE -ne 0) { throw "Could not duplicate Ultimate Performance plan: $output" }
          $guid = [regex]::Match(($output -join ' '), '[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}').Value;
          if (-not $guid) { throw 'Windows did not return the new power-plan GUID' }
          powercfg /setactive $guid | Out-Null;
          if ($LASTEXITCODE -ne 0) { throw "Could not activate power plan $guid" }
          Set-Content -LiteralPath (Join-Path $env:TEMP 'ca-o-powerplan.guid') -Value $guid -Encoding ascii;
          Write-Output "Ultimate Performance plan enabled and activated ($guid)"
        `
      }
    ],
    rebootRequired: false
  },
  'gaming-mode': {
    commands: [
      {
        description: "Enabling Game Mode",
        script: `
          Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\GameBar' -Name 'AllowAutoGameMode' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue;
          Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\GameBar' -Name 'AutoGameModeEnabled' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue;
          Write-Output 'Game mode enabled'
        `
      }
    ],
    rebootRequired: false
  },
  'disable-services': {
    commands: [
      {
        description: "Disabling Non-Essential Services (Maps, Fax, XPS)",
        script: `
          Stop-Service -Name 'MapsBroker' -Force -ErrorAction SilentlyContinue; Set-Service -Name 'MapsBroker' -StartupType Disabled -ErrorAction SilentlyContinue;
          Stop-Service -Name 'Fax' -Force -ErrorAction SilentlyContinue; Set-Service -Name 'Fax' -StartupType Disabled -ErrorAction SilentlyContinue;
          Write-Output 'Bloatware services disabled'
        `
      }
    ],
    rebootRequired: true
  },
  'registry-cleanup': {
    commands: [
      {
        description: "Registry Cleanup (Safe)",
        script: `
          Remove-Item -Path "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RunMRU\\*" -Force -ErrorAction SilentlyContinue
          Remove-Item -Path "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\TypedPaths\\*" -Force -ErrorAction SilentlyContinue
          Write-Output 'Basic registry cleanup completed (MRU lists cleared)'
        `
      }
    ],
    rebootRequired: false
  },
  'memory-compression': {
    commands: [
      {
        description: "Disabling Memory Compression",
        script: `
          Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue;
          Write-Output 'Memory Compression disabled'
        `
      }
    ],
    rebootRequired: true
  }
};

const additionalRealCommands: typeof baseRealCommands = {
  'disable-error-reporting': { commands: [{ description: 'Disabling Windows Error Reporting', script: "Set-Service -Name WerSvc -StartupType Disabled -ErrorAction Stop; Stop-Service -Name WerSvc -Force -ErrorAction Stop" }], rebootRequired: false },
  'disable-delivery-optimization': { commands: [{ description: 'Disabling Delivery Optimization service', script: "Set-Service -Name DoSvc -StartupType Disabled -ErrorAction Stop; Stop-Service -Name DoSvc -Force -ErrorAction Stop" }], rebootRequired: false },
  'disable-windows-insider': { commands: [{ description: 'Disabling Windows Insider service', script: "if (Get-Service -Name wisvc -ErrorAction SilentlyContinue) { Set-Service -Name wisvc -StartupType Disabled -ErrorAction Stop; Stop-Service -Name wisvc -Force -ErrorAction Stop }" }], rebootRequired: false },
  'disable-retail-demo': { commands: [{ description: 'Disabling Retail Demo service', script: "if (Get-Service -Name RetailDemo -ErrorAction SilentlyContinue) { Set-Service -Name RetailDemo -StartupType Disabled -ErrorAction Stop; Stop-Service -Name RetailDemo -Force -ErrorAction Stop }" }], rebootRequired: false },
  'disable-network-throttling': { commands: [{ description: 'Disabling multimedia network throttling', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name NetworkThrottlingIndex -Value 4294967295 -Type DWord -Force" }], rebootRequired: true },
  'optimize-network-power': { commands: [{ description: 'Disabling network adapter power saving', script: "Get-NetAdapter -Physical | ForEach-Object { Set-NetAdapterPowerManagement -Name $_.Name -AllowComputerToTurnOffDevice Disabled -ErrorAction Stop }" }], rebootRequired: false },
  'enable-mouse-raw-input': { commands: [{ description: 'Enabling raw mouse input policy', script: "Write-Output 'Raw mouse input is controlled by each application and device driver; no global Windows switch exists.'" }], rebootRequired: false },
  'disable-lock-screen': { commands: [{ description: 'Disabling lock screen policy', script: "$path='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization'; New-Item -Path $path -Force | Out-Null; Set-ItemProperty -Path $path -Name NoLockScreen -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'disable-aero-peek': { commands: [{ description: 'Disabling Aero Peek preview', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-startup-sound': { commands: [{ description: 'Disabling Windows startup sound', script: "Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -Value '' -Force" }], rebootRequired: false },
  'disable-cast-notifications': { commands: [{ description: 'Disabling wireless display notifications', script: "$path='HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting'; New-Item -Path $path -Force | Out-Null; Set-ItemProperty -Path $path -Name AllowWhileLocked -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-background-apps': { commands: [{ description: 'Disabling background app policy', script: "$backgroundAppsPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications'; if (-not (Test-Path $backgroundAppsPath)) { New-Item -Path $backgroundAppsPath -Force | Out-Null }; Set-ItemProperty -Path $backgroundAppsPath -Name GlobalUserDisabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-start-menu-suggestions': { commands: [{ description: 'Disabling Start menu suggestions', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-game-dvr': { commands: [{ description: 'Disabling Game DVR capture', script: "Set-ItemProperty -Path 'HKCU:\\System\\GameConfigStore' -Name GameDVR_Enabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-power-throttling': { commands: [{ description: 'Disabling power throttling', script: "$throttlingPath = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling'; if (-not (Test-Path $throttlingPath)) { New-Item -Path $throttlingPath -Force | Out-Null }; Set-ItemProperty -Path $throttlingPath -Name PowerThrottlingOff -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'enable-msi-gpu': { commands: [{ description: 'Checking MSI support for display devices', script: "$devices=Get-PnpDevice -Class Display -Status OK; if (-not $devices) { throw 'No active display device found' }; Write-Output ($devices | Select-Object -ExpandProperty FriendlyName); Write-Output 'MSI must be enabled by the display driver and is not changed by this tool.'" }], rebootRequired: false },
  'disable-cpu-idle': { commands: [{ description: 'Selecting processor performance energy policy', script: 'powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100; powercfg /setactive SCHEME_CURRENT' }], rebootRequired: false },
  'disable-core-parking': { commands: [{ description: 'Disabling processor core parking', script: 'powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100; powercfg /setactive SCHEME_CURRENT' }], rebootRequired: false },
  'disable-memory-dumps': { commands: [{ description: 'Disabling automatic kernel memory dumps', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\CrashControl' -Name CrashDumpEnabled -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'enable-long-paths': { commands: [{ description: 'Enabling Win32 long paths', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name LongPathsEnabled -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'timer-resolution-0-5ms': { commands: [{ description: 'Requesting the lowest supported Windows timer resolution', script: "if (-not ('CAOTimerNativeMethods' -as [type])) { Add-Type @'\nusing System;\nusing System.Runtime.InteropServices;\npublic static class CAOTimerNativeMethods {\n  [DllImport(\"ntdll.dll\")] public static extern uint NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);\n}\n'@ }; $scriptPath=Join-Path $env:TEMP 'ca-o-timer-resolution.ps1'; $pidPath=Join-Path $env:TEMP 'ca-o-timer-resolution.pid'; @'\nAdd-Type @\"\nusing System;\nusing System.Runtime.InteropServices;\npublic static class CAOTimerNativeMethods {\n  [DllImport(\"ntdll.dll\")] public static extern uint NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);\n}\n\"@\n[uint32]$current=0\n$status=[CAOTimerNativeMethods]::NtSetTimerResolution(5000,$true,[ref]$current)\nif ($status -ne 0) { throw \"NtSetTimerResolution failed: $status\" }\nSet-Content -LiteralPath \"$env:TEMP\\ca-o-timer-resolution.pid\" -Value $PID -Encoding ascii\ntry { $deadline=(Get-Date).AddHours(2); while ((Get-Date) -lt $deadline) { Start-Sleep -Seconds 30 } } finally { [CAOTimerNativeMethods]::NtSetTimerResolution(5000,$false,[ref]$current) | Out-Null; Remove-Item -LiteralPath \"$env:TEMP\\ca-o-timer-resolution.pid\" -Force -ErrorAction SilentlyContinue }\n'@ | Set-Content -Path $scriptPath -Encoding UTF8; if (Test-Path $pidPath) { $oldPid=Get-Content $pidPath -ErrorAction SilentlyContinue; if ($oldPid) { Stop-Process -Id ([int]$oldPid) -Force -ErrorAction SilentlyContinue } }; Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @('-NoProfile','-NonInteractive','-EncodedCommand',([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes([System.IO.File]::ReadAllText($scriptPath))))); Start-Sleep -Milliseconds 800; if (-not (Test-Path $pidPath)) { throw 'Timer resolution helper did not start' }" }], rebootRequired: false },
  'remove-onedrive': { commands: [{ description: 'Uninstalling OneDrive from this Windows user', script: "$setups = @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDriveSetup.exe\",\"$env:SystemRoot\\SysWOW64\\OneDriveSetup.exe\",\"$env:SystemRoot\\System32\\OneDriveSetup.exe\") | Where-Object { Test-Path $_ }; if (-not $setups) { throw 'OneDrive installer was not found' }; Stop-Process -Name OneDrive -Force -ErrorAction SilentlyContinue; & $setups[0] /uninstall | Out-Null; if ($LASTEXITCODE -notin @(0,3010,1641,1605)) { throw \"OneDrive uninstall failed with exit code $LASTEXITCODE\" }" }], rebootRequired: true },
  'disable-advertising-id': { commands: [{ description: 'Disabling Windows Advertising ID', script: "$advertisingPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo'; if (-not (Test-Path $advertisingPath)) { New-Item -Path $advertisingPath -Force | Out-Null }; Set-ItemProperty -Path $advertisingPath -Name Enabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-tailored-experiences': { commands: [{ description: 'Disabling tailored experiences', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-activity-history': { commands: [{ description: 'Disabling activity history collection', script: "$path='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System'; New-Item -Path $path -Force | Out-Null; Set-ItemProperty -Path $path -Name PublishUserActivities -Value 0 -Type DWord -Force; Set-ItemProperty -Path $path -Name UploadUserActivities -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'disable-location-tracking': { commands: [{ description: 'Disabling Windows location tracking', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location' -Name Value -Value 'Deny' -Force" }], rebootRequired: true },
  'disable-windows-feedback': { commands: [{ description: 'Disabling Windows feedback prompts', script: "$siufPath = 'HKCU:\\Software\\Microsoft\\Siuf\\Rules'; if (-not (Test-Path $siufPath)) { New-Item -Path $siufPath -Force | Out-Null }; Set-ItemProperty -Path $siufPath -Name NumberOfSIUFInPeriod -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-cloud-content': { commands: [{ description: 'Disabling consumer cloud content', script: "$p='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent'; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name DisableConsumerAccountStateContent -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'disable-app-suggestions': { commands: [{ description: 'Disabling app suggestion data', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-start-tracking': { commands: [{ description: 'Disabling Start app tracking', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-setting-sync': { commands: [{ description: 'Disabling settings synchronization', script: "$settingSyncPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync'; if (-not (Test-Path $settingSyncPath)) { New-Item -Path $settingSyncPath -Force | Out-Null }; Set-ItemProperty -Path $settingSyncPath -Name SyncPolicy -Value 5 -Type DWord -Force" }], rebootRequired: false },
  'disable-input-personalization': { commands: [{ description: 'Disabling input personalization', script: "$p='HKCU:\\Software\\Microsoft\\InputPersonalization'; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name RestrictImplicitInkCollection -Value 1 -Type DWord -Force; Set-ItemProperty -Path $p -Name RestrictImplicitTextCollection -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-handwriting-data': { commands: [{ description: 'Disabling handwriting data sharing', script: "$p='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC'; New-Item -Path $p -Force | Out-Null; Set-ItemProperty -Path $p -Name PreventHandwritingDataSharing -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'disable-speech-recognition': { commands: [{ description: 'Disabling online speech recognition', script: "$speechPath = 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy'; if (-not (Test-Path $speechPath)) { New-Item -Path $speechPath -Force | Out-Null }; Set-ItemProperty -Path $speechPath -Name HasAccepted -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-find-my-device': { commands: [{ description: 'Disabling Find My Device', script: "$findMyDevicePath = 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice'; if (-not (Test-Path $findMyDevicePath)) { New-Item -Path $findMyDevicePath -Force | Out-Null }; Set-ItemProperty -Path $findMyDevicePath -Name LocationSyncEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-contacts-access': { commands: [{ description: 'Denying contacts access to apps', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\contacts' -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'disable-calendar-access': { commands: [{ description: 'Denying calendar access to apps', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appointments' -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'disable-camera-access': { commands: [{ description: 'Denying camera access to apps', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'disable-microphone-access': { commands: [{ description: 'Denying microphone access to apps', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'disable-llmnr': { commands: [{ description: 'Disabling LLMNR', script: "$path='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient'; New-Item -Path $path -Force | Out-Null; Set-ItemProperty -Path $path -Name EnableMulticast -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'menu-delay': { commands: [{ description: 'Reducing menu delay', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MenuShowDelay -Value '0' -Force" }], rebootRequired: false },
  'inactive-window-scroll': { commands: [{ description: 'Disabling inactive window scrolling', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MouseWheelRouting -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-sticky-keys': { commands: [{ description: 'Disabling Sticky Keys shortcut', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Accessibility\\StickyKeys' -Name Flags -Value '506' -Force" }], rebootRequired: false },
  'disable-usb-suspend': { commands: [{ description: 'Disabling USB selective suspend', script: "powercfg /setacvalueindex SCHEME_CURRENT SUB_USB USBSELECTIVE 0; powercfg /setactive SCHEME_CURRENT" }], rebootRequired: false },
  'show-file-extensions': { commands: [{ description: 'Showing file extensions', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -Value 0 -Type DWord -Force; Stop-Process -Name explorer -Force" }], rebootRequired: false },
  'disable-thumbnails': { commands: [{ description: 'Disabling file thumbnails', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -Value 1 -Type DWord -Force; Stop-Process -Name explorer -Force" }], rebootRequired: false },
  'disable-tooltips': { commands: [{ description: 'Disabling Explorer tooltips', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-wallpaper-slideshow': { commands: [{ description: 'Disabling wallpaper slideshow', script: "$slideshowPath = 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow'; if (-not (Test-Path $slideshowPath)) { New-Item -Path $slideshowPath -Force | Out-Null }; Set-ItemProperty -Path $slideshowPath -Name Interval -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-system-sounds': { commands: [{ description: 'Disabling system sounds', script: "Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -Value '.None' -Force" }], rebootRequired: false },
  'show-hidden-files': { commands: [{ description: 'Showing hidden files', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'hide-task-view': { commands: [{ description: 'Hiding Task View button', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-taskbar-search': { commands: [{ description: 'Reducing taskbar search footprint', script: "$searchPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search'; if (-not (Test-Path $searchPath)) { New-Item -Path $searchPath -Force | Out-Null }; Set-ItemProperty -Path $searchPath -Name SearchboxTaskbarMode -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-hibernation': { commands: [{ description: 'Disabling hibernation', script: 'powercfg /hibernate off' }], rebootRequired: false },
  'disable-fast-startup': { commands: [{ description: 'Disabling Fast Startup', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'enable-hags': { commands: [{ description: 'Enabling Hardware Accelerated GPU Scheduling', script: "New-Item -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' -Force | Out-Null; Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' -Name HwSchMode -Value 2 -Type DWord -Force" }], rebootRequired: true },
  'disable-bits': { commands: [{ description: 'Disabling Background Intelligent Transfer Service', script: "Set-Service -Name BITS -StartupType Disabled -ErrorAction Stop; Stop-Service -Name BITS -Force -ErrorAction Stop" }], rebootRequired: false },

  // ─── NEW BATCH (2026-08) ──────────────────────────────────────────────
  'disable-widgets': { commands: [{ description: 'Disabling Widgets panel and taskbar button', script: "$dshPath = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Dsh'; if (-not (Test-Path $dshPath)) { New-Item -Path $dshPath -Force | Out-Null }; Set-ItemProperty -Path $dshPath -Name AllowNewsAndInterests -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarDa -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-recall': { commands: [{ description: 'Disabling Recall and AI activity analysis', script: "$aiPolicyPath = 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsAI'; if (-not (Test-Path $aiPolicyPath)) { New-Item -Path $aiPolicyPath -Force | Out-Null }; Set-ItemProperty -Path $aiPolicyPath -Name DisableAIDataAnalysis -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'disable-netbios': { commands: [{ description: 'Disabling NetBIOS over TCP/IP on every interface', script: "$netbtInterfaces = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NetBT\\Parameters\\Interfaces'; foreach ($iface in $netbtInterfaces) { Set-ItemProperty -Path $iface.PSPath -Name NetbiosOptions -Value 2 -Type DWord -Force }" }], rebootRequired: true },
  'disable-smb1': { commands: [{ description: 'Disabling the deprecated SMBv1 server protocol', script: "Set-SmbServerConfiguration -EnableSMB1Protocol $false -Confirm:$false -ErrorAction Stop" }], rebootRequired: false },
  'show-seconds-clock': { commands: [{ description: 'Showing seconds in the taskbar clock', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowSecondsInSystemClock -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'hide-meet-now': { commands: [{ description: 'Hiding the Meet Now taskbar icon', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowMeetNow -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-fullscreen-optimizations': { commands: [{ description: 'Disabling fullscreen optimizations globally', script: "$fsoPath = 'HKCU:\\System\\GameConfigStore'; if (-not (Test-Path $fsoPath)) { New-Item -Path $fsoPath -Force | Out-Null }; Set-ItemProperty -Path $fsoPath -Name GameDVR_FSOBehavior -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-welcome-experience': { commands: [{ description: 'Disabling the Windows welcome experience', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -Value 0 -Type DWord -Force" }], rebootRequired: false },

  // ─── CATEGORY BATCH x10 EACH (2026-08) ────────────────────────────────
  // SYSTEM
  'disable-ceip-tasks': { commands: [{ description: 'Disabling CEIP and Application Experience scheduled tasks', script: "$ceipTasks = @(@('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','Consolidator'), @('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','UsbCeip'), @('\\Microsoft\\Windows\\Application Experience\\','Microsoft Compatibility Appraiser'), @('\\Microsoft\\Windows\\Application Experience\\','ProgramDataUpdater')); foreach ($entry in $ceipTasks) { Disable-ScheduledTask -TaskPath $entry[0] -TaskName $entry[1] -ErrorAction SilentlyContinue | Out-Null }" }], rebootRequired: false },
  'disable-last-access-time': { commands: [{ description: 'Disabling NTFS last-access timestamp updates', script: "fsutil behavior set disablelastaccess 1 | Out-Null" }], rebootRequired: true },
  'disable-8dot3-names': { commands: [{ description: 'Disabling 8.3 short name creation on NTFS volumes', script: "fsutil behavior set disable8dot3 1 | Out-Null" }], rebootRequired: true },
  'disable-admin-shares': { commands: [{ description: 'Disabling default administrative shares (C$, ADMIN$)', script: "$shareParams = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters'; if (-not (Test-Path $shareParams)) { New-Item -Path $shareParams -Force | Out-Null }; Set-ItemProperty -Path $shareParams -Name AutoShareServer -Value 0 -Type DWord -Force; Set-ItemProperty -Path $shareParams -Name AutoShareWks -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'disable-remote-assistance': { commands: [{ description: 'Disabling Remote Assistance invitations', script: "$raKey = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance'; if (-not (Test-Path $raKey)) { New-Item -Path $raKey -Force | Out-Null }; Set-ItemProperty -Path $raKey -Name fAllowToGetHelp -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-remote-desktop': { commands: [{ description: 'Blocking incoming Remote Desktop connections', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server' -Name fDenyTSConnections -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'speedup-shutdown': { commands: [{ description: 'Reducing service shutdown wait and auto-ending apps', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name WaitToKillServiceTimeout -Value '2000' -Force; Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name AutoEndTasks -Value '1' -Force" }], rebootRequired: false },
  'no-auto-reboot-active': { commands: [{ description: 'Preventing automatic reboot while a user is signed in', script: "$auPath = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU'; if (-not (Test-Path $auPath)) { New-Item -Path $auPath -Force | Out-Null }; Set-ItemProperty -Path $auPath -Name NoAutoRebootWithLoggedOnUsers -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-driver-search': { commands: [{ description: 'Stopping Windows Update driver searches', script: "$dsPath = 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching'; if (-not (Test-Path $dsPath)) { New-Item -Path $dsPath -Force | Out-Null }; Set-ItemProperty -Path $dsPath -Name SearchOrderConfig -Value 0 -Type DWord -Force" }], rebootRequired: false },

  // NETWORK
  'flush-arp-cache': { commands: [{ description: 'Flushing the local ARP table', script: "arp -d * | Out-Null; Write-Output 'ARP cache flushed'" }], rebootRequired: false },
  'disable-hotspot-service': { commands: [{ description: 'Disabling the Mobile Hotspot service', script: "if (Get-Service -Name icssvc -ErrorAction SilentlyContinue) { Set-Service -Name icssvc -StartupType Disabled -ErrorAction Stop; Stop-Service -Name icssvc -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },
  'require-network-level-auth': { commands: [{ description: 'Requiring NLA for Remote Desktop sessions', script: "$rdpTcp = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp'; if (-not (Test-Path $rdpTcp)) { New-Item -Path $rdpTcp -Force | Out-Null }; Set-ItemProperty -Path $rdpTcp -Name UserAuthentication -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-wpad': { commands: [{ description: 'Disabling Web Proxy Auto-Discovery', script: "if (Get-Service -Name WinHttpAutoProxySvc -ErrorAction SilentlyContinue) { Set-Service -Name WinHttpAutoProxySvc -StartupType Disabled -ErrorAction Stop; Stop-Service -Name WinHttpAutoProxySvc -Force -ErrorAction SilentlyContinue }" }], rebootRequired: true },
  'disable-active-probing': { commands: [{ description: 'Disabling NCSI internet connectivity probes', script: "$nlaInternet = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NlaSvc\\Parameters\\Internet'; Set-ItemProperty -Path $nlaInternet -Name EnableActiveProbing -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-peer-name-resolution': { commands: [{ description: 'Disabling the Peer Name Resolution Protocol service', script: "if (Get-Service -Name PNRPsvc -ErrorAction SilentlyContinue) { Set-Service -Name PNRPsvc -StartupType Disabled -ErrorAction Stop; Stop-Service -Name PNRPsvc -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },
  'restrict-point-and-print': { commands: [{ description: 'Restricting printer driver installs to administrators', script: "$pap = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\Printers\\PointAndPrint'; if (-not (Test-Path $pap)) { New-Item -Path $pap -Force | Out-Null }; Set-ItemProperty -Path $pap -Name RestrictDriverInstallationToAdministrators -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-ssdp-discovery': { commands: [{ description: 'Disabling SSDP discovery traffic', script: "if (Get-Service -Name SSDPSRV -ErrorAction SilentlyContinue) { Set-Service -Name SSDPSRV -StartupType Disabled -ErrorAction Stop; Stop-Service -Name SSDPSRV -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },
  'disable-upnp-device-host': { commands: [{ description: 'Disabling the UPnP Device Host service', script: "if (Get-Service -Name upnphost -ErrorAction SilentlyContinue) { Set-Service -Name upnphost -StartupType Disabled -ErrorAction Stop; Stop-Service -Name upnphost -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },
  'disable-snmp-trap': { commands: [{ description: 'Disabling the SNMP Trap service', script: "if (Get-Service -Name SNMPTRAP -ErrorAction SilentlyContinue) { Set-Service -Name SNMPTRAP -StartupType Disabled -ErrorAction Stop; Stop-Service -Name SNMPTRAP -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },

  // INPUT
  'disable-filter-keys': { commands: [{ description: 'Disabling the Filter Keys shortcut', script: "$fk = 'HKCU:\\Control Panel\\Accessibility\\FilterKeys'; if (-not (Test-Path $fk)) { New-Item -Path $fk -Force | Out-Null }; Set-ItemProperty -Path $fk -Name Flags -Value '122' -Force" }], rebootRequired: false },
  'disable-toggle-keys': { commands: [{ description: 'Disabling the Toggle Keys shortcut', script: "$tk = 'HKCU:\\Control Panel\\Accessibility\\ToggleKeys'; if (-not (Test-Path $tk)) { New-Item -Path $tk -Force | Out-Null }; Set-ItemProperty -Path $tk -Name Flags -Value '58' -Force" }], rebootRequired: false },
  'disable-touch-keyboard-autoinvoke': { commands: [{ description: 'Stopping the touch keyboard from auto-invoking', script: "$tt = 'HKCU:\\Software\\Microsoft\\TabletTip\\1.7'; if (-not (Test-Path $tt)) { New-Item -Path $tt -Force | Out-Null }; Set-ItemProperty -Path $tt -Name EnableDesktopModeAutoInvoke -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-controller-gamebar-chord': { commands: [{ description: 'Disabling the controller Xbox button Game Bar chord', script: "$gb = 'HKCU:\\Software\\Microsoft\\GameBar'; if (-not (Test-Path $gb)) { New-Item -Path $gb -Force | Out-Null }; Set-ItemProperty -Path $gb -Name UseNexusForGameBarEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-touchpad-edge-swipes': { commands: [{ description: 'Disabling precision touchpad edge swipes', script: "$ptp = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad'; if (-not (Test-Path $ptp)) { New-Item -Path $ptp -Force | Out-Null }; Set-ItemProperty -Path $ptp -Name EdgeSwipeEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-touchpad-threefinger-slide': { commands: [{ description: 'Disabling three-finger slide gestures', script: "$ptp = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad'; if (-not (Test-Path $ptp)) { New-Item -Path $ptp -Force | Out-Null }; Set-ItemProperty -Path $ptp -Name ThreeFingerSlideEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-windows-ink': { commands: [{ description: 'Disabling Windows Ink workspace via policy', script: "$inkPolicy = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsInkWorkspace'; if (-not (Test-Path $inkPolicy)) { New-Item -Path $inkPolicy -Force | Out-Null }; Set-ItemProperty -Path $inkPolicy -Name AllowWindowsInkWorkspace -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'numlock-on-boot': { commands: [{ description: 'Forcing NumLock on at sign-in and boot', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -Value '2' -Force; Set-ItemProperty -Path 'Registry::HKEY_USERS\\.DEFAULT\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -Value '2' -Force" }], rebootRequired: false },
  'disable-hover-checkboxes': { commands: [{ description: 'Disabling hover item checkboxes in Explorer', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name AutoCheckSelect -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-tablet-input-service': { commands: [{ description: 'Disabling the Touch Keyboard and Handwriting service', script: "if (Get-Service -Name TabletInputService -ErrorAction SilentlyContinue) { Set-Service -Name TabletInputService -StartupType Disabled -ErrorAction Stop; Stop-Service -Name TabletInputService -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },

  // TWEAKS
  'delay-taskbar-thumbnails': { commands: [{ description: 'Delaying taskbar thumbnail previews', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ExtendedUIHoverTime -Value 30000 -Type DWord -Force" }], rebootRequired: false },
  'hide-start-recommended': { commands: [{ description: 'Hiding the Start menu recommendations section', script: "$hrPolicy = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer'; if (-not (Test-Path $hrPolicy)) { New-Item -Path $hrPolicy -Force | Out-Null }; Set-ItemProperty -Path $hrPolicy -Name HideRecommendedSection -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-drag-full-window': { commands: [{ description: 'Showing outlines instead of contents while dragging', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name DragFullWindows -Value '0' -Force" }], rebootRequired: false },
  'never-combine-taskbar-icons': { commands: [{ description: 'Never combining taskbar buttons with labels', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarGlomLevel -Value 2 -Type DWord -Force" }], rebootRequired: false },
  'disable-window-shake': { commands: [{ description: 'Disabling minimize-on-shake window gesture', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisableViewShake -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-snap-layouts-flyout': { commands: [{ description: 'Disabling the snap layouts flyout on maximize', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name EnableSnapAssistFlyout -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-window-arrange-drag': { commands: [{ description: 'Disabling automatic window arrangement while dragging', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name WindowArrangementActive -Value '0' -Force" }], rebootRequired: false },
  'disable-spotlight-wallpapers': { commands: [{ description: 'Switching lock screen Spotlight to a static image', script: "$cdmKey = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager'; Set-ItemProperty -Path $cdmKey -Name RotatingLockScreenEnabled -Value 0 -Type DWord -Force; Set-ItemProperty -Path $cdmKey -Name RotatingLockScreenOverlayEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'hide-copilot-button': { commands: [{ description: 'Hiding the Copilot taskbar button via policy', script: "$copilotPolicy = 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot'; if (-not (Test-Path $copilotPolicy)) { New-Item -Path $copilotPolicy -Force | Out-Null }; Set-ItemProperty -Path $copilotPolicy -Name TurnOffWindowsCopilot -Value 1 -Type DWord -Force" }], rebootRequired: false },

  // POWERFUL
  'optimize-thread-scheduling': { commands: [{ description: 'Setting Win32PrioritySeparation to short quantum with foreground boost', script: "$priorityControl = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl'; if (-not (Test-Path $priorityControl)) { New-Item -Path $priorityControl -Force | Out-Null }; Set-ItemProperty -Path $priorityControl -Name Win32PrioritySeparation -Value 38 -Type DWord -Force" }], rebootRequired: true },
  'disable-svchost-split-threshold': { commands: [{ description: 'Grouping svchost services into shared processes', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name SvcHostSplitThresholdInKB -Value (-1) -Type DWord -Force" }], rebootRequired: true },
  'optimize-ntfs-memory-usage': { commands: [{ description: 'Increasing the NTFS paged-pool memory usage level', script: "fsutil behavior set memoryusage 2 | Out-Null" }], rebootRequired: true },
  'disable-modern-standby': { commands: [{ description: 'Reverting Modern Standby (S0) to classic sleep states', script: "$powerKey = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power'; if (-not (Test-Path $powerKey)) { New-Item -Path $powerKey -Force | Out-Null }; Set-ItemProperty -Path $powerKey -Name PlatformAoAcOverride -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'disable-edge-startup-boost': { commands: [{ description: 'Disabling Microsoft Edge startup boost', script: "$edgePolicy = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge'; if (-not (Test-Path $edgePolicy)) { New-Item -Path $edgePolicy -Force | Out-Null }; Set-ItemProperty -Path $edgePolicy -Name StartupBoostEnabled -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-automatic-maintenance': { commands: [{ description: 'Disabling idle-time automatic maintenance', script: "$maintenanceKey = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Schedule\\Maintenance'; if (-not (Test-Path $maintenanceKey)) { New-Item -Path $maintenanceKey -Force | Out-Null }; Set-ItemProperty -Path $maintenanceKey -Name MaintenanceDisabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-app-readiness': { commands: [{ description: 'Disabling the App Readiness service', script: "if (Get-Service -Name AppReadiness -ErrorAction SilentlyContinue) { Set-Service -Name AppReadiness -StartupType Disabled -ErrorAction Stop; Stop-Service -Name AppReadiness -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },
  'disable-ssl-time-seeding': { commands: [{ description: 'Disabling SSL-based secure time seeding', script: "$w32Config = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\W32Time\\Config'; if (-not (Test-Path $w32Config)) { New-Item -Path $w32Config -Force | Out-Null }; Set-ItemProperty -Path $w32Config -Name UtilizeSslTimeData -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'max-system-responsiveness': { commands: [{ description: 'Reserving zero percent CPU for background multimedia tasks', script: "$sysProfile = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile'; if (-not (Test-Path $sysProfile)) { New-Item -Path $sysProfile -Force | Out-Null }; Set-ItemProperty -Path $sysProfile -Name SystemResponsiveness -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-memory-integrity': { commands: [{ description: 'Disabling Memory Integrity (HVCI) for maximum game performance', script: "$hvci = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity'; if (-not (Test-Path $hvci)) { New-Item -Path $hvci -Force | Out-Null }; Set-ItemProperty -Path $hvci -Name Enabled -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'windowed-games-optimization': { commands: [{ description: 'Enabling Optimizations for Windowed Games (swap upgrade)', script: "$gpuPref = 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences'; if (-not (Test-Path $gpuPref)) { New-Item -Path $gpuPref -Force | Out-Null }; $settings = (Get-ItemProperty -Path $gpuPref -Name DirectXUserGlobalSettings -ErrorAction SilentlyContinue).DirectXUserGlobalSettings; if (-not $settings) { $settings = '' }; if ($settings -match 'SwapEffectUpgradeEnable=\\d+') { $settings = $settings -replace 'SwapEffectUpgradeEnable=\\d+', 'SwapEffectUpgradeEnable=1' } elseif ($settings) { $settings = $settings.TrimEnd(';') + ';SwapEffectUpgradeEnable=1' } else { $settings = 'SwapEffectUpgradeEnable=1;' }; Set-ItemProperty -Path $gpuPref -Name DirectXUserGlobalSettings -Value $settings -Force" }], rebootRequired: false },
  'disable-multiplane-overlay': { commands: [{ description: 'Disabling Multi-Plane Overlay to fix frame pacing', script: "$dwm = 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm'; if (-not (Test-Path $dwm)) { New-Item -Path $dwm -Force | Out-Null }; Set-ItemProperty -Path $dwm -Name OverlayTestMode -Value 5 -Type DWord -Force" }], rebootRequired: true },
  'static-pagefile': { commands: [{ description: 'Configuring a fixed-size pagefile on the system drive', script: "$memKey = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management'; $ramMb = [int][Math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1MB); $pageMb = [int][Math]::Round($ramMb * 1.5 / 1024) * 1024; Set-ItemProperty -Path $memKey -Name PagingFiles -Value @(\"$env:SystemDrive\\pagefile.sys $pageMb $pageMb\") -Type MultiString -Force" }], rebootRequired: true },
  'uninstall-copilot': { commands: [{ description: 'Uninstalling Microsoft Copilot app packages', script: "$copilotPatterns = @('*Microsoft.Windows.Ai.Copilot*','*Microsoft.Copilot*'); foreach ($pattern in $copilotPatterns) { Get-AppxPackage -Name $pattern -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxPackage -AllUsers -Name $pattern -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue }; Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like '*Copilot*' } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null }; Write-Output 'Copilot packages removed'" }], rebootRequired: false },
  'uninstall-bing-search': { commands: [{ description: 'Uninstalling the Bing Search app', script: "Get-AppxPackage -Name '*Microsoft.BingSearch*' -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxPackage -AllUsers -Name '*Microsoft.BingSearch*' -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like '*Bing Search*' } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null }; Write-Output 'Bing Search removed'" }], rebootRequired: false },
  'disable-click-to-do': { commands: [{ description: 'Blocking Click to Do AI analysis via policy', script: "$aiPolicyPaths = @('HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsAI','HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI'); foreach ($aiPath in $aiPolicyPaths) { if (-not (Test-Path $aiPath)) { New-Item -Path $aiPath -Force | Out-Null }; Set-ItemProperty -Path $aiPath -Name DisableClickToDo -Value 1 -Type DWord -Force }" }], rebootRequired: false },
  'disable-paint-ai': { commands: [{ description: 'Disabling Paint Cocreator and Image Generator', script: "$paintPolicy = 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Paint'; if (-not (Test-Path $paintPolicy)) { New-Item -Path $paintPolicy -Force | Out-Null }; Set-ItemProperty -Path $paintPolicy -Name DisableCocreator -Value 1 -Type DWord -Force; Set-ItemProperty -Path $paintPolicy -Name DisableImageGenerator -Value 1 -Type DWord -Force" }], rebootRequired: false },

  // PRIVACY
  'disable-clipboard-history': { commands: [{ description: 'Disabling local clipboard history', script: "$clipboardKey = 'HKCU:\\Software\\Microsoft\\Clipboard'; if (-not (Test-Path $clipboardKey)) { New-Item -Path $clipboardKey -Force | Out-Null }; Set-ItemProperty -Path $clipboardKey -Name EnableClipboardHistory -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-clipboard-cloud-sync': { commands: [{ description: 'Blocking clipboard upload to the cloud', script: "$clipboardKey = 'HKCU:\\Software\\Microsoft\\Clipboard'; if (-not (Test-Path $clipboardKey)) { New-Item -Path $clipboardKey -Force | Out-Null }; Set-ItemProperty -Path $clipboardKey -Name CloudClipboardAutomaticUpload -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'deny-user-account-information': { commands: [{ description: 'Denying apps access to account information', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\userAccountInformation'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-documents-library': { commands: [{ description: 'Denying apps access to the Documents library', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\documentsLibrary'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-pictures-library': { commands: [{ description: 'Denying apps access to the Pictures library', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\picturesLibrary'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-videos-library': { commands: [{ description: 'Denying apps access to the Videos library', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\videosLibrary'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-email-access': { commands: [{ description: 'Denying apps access to email data', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\email'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-radios-access': { commands: [{ description: 'Denying apps control over device radios', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\radios'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-human-presence': { commands: [{ description: 'Denying apps access to presence sensors', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\humanPresence'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
  'deny-broad-filesystem': { commands: [{ description: 'Denying broad filesystem access to apps', script: "$capPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\broadFileSystemAccess'; if (-not (Test-Path $capPath)) { New-Item -Path $capPath -Force | Out-Null }; Set-ItemProperty -Path $capPath -Name Value -Value 'Deny' -Force" }], rebootRequired: false },
};

export const realCommands = { ...baseRealCommands, ...additionalRealCommands };

export const sessionScopedOptimizationIds = new Set(['timer-resolution-0-5ms']);

/**
 * Maintenance actions with no persistent Windows state to protect: they can be
 * run again at any time and their "revert" only clears the tracked state.
 */
export const repeatableOptimizationIds = new Set(['clear-temp-files', 'flush-dns', 'timer-resolution-0-5ms', 'flush-arp-cache']);

export const irreversibleOptimizationIds = new Set(['remove-onedrive', 'winsock-reset', 'flush-dns', 'reset-network', 'clear-temp-files', 'registry-cleanup', 'flush-arp-cache', 'uninstall-copilot', 'uninstall-bing-search']);

export const securityImpactById: Record<string, 'none' | 'low' | 'medium' | 'high' | 'reduces-security'> = {
  'disable-services': 'low',
  'disable-bits': 'low',
  'disable-print-spooler': 'low',
  'disable-location-tracking': 'low',
  'disable-camera-access': 'low',
  'disable-microphone-access': 'low',
  'disable-contacts-access': 'low',
  'disable-calendar-access': 'low',
  'remove-onedrive': 'medium',
  'disable-memory-integrity': 'reduces-security',
};

/**
 * Realistic per-optimization performance impact used by the API and the UI
 * (instead of the old everything-"medium" placeholder).
 */
export const performanceImpactById: Record<string, 'low' | 'medium' | 'high' | 'very-high'> = {
  // Very high: big, measurable wins on modern machines
  'disable-recall': 'very-high',
  'memory-compression': 'very-high',
  'disable-svchost-split-threshold': 'very-high',
  'power-plan': 'very-high',
  'disable-cpu-idle': 'very-high',
  'disable-core-parking': 'very-high',
  'disable-memory-integrity': 'very-high',
  'uninstall-copilot': 'medium',
  'uninstall-bing-search': 'low',
  'disable-click-to-do': 'low',
  'disable-paint-ai': 'low',
  'windowed-games-optimization': 'medium',
  'disable-multiplane-overlay': 'medium',
  'static-pagefile': 'medium',
  // High: clear FPS/latency/frame-time effect
  'enable-hags': 'high',
  'disable-game-dvr': 'high',
  'disable-fullscreen-optimizations': 'high',
  'disable-power-throttling': 'high',
  'gaming-mode': 'high',
  'max-system-responsiveness': 'high',
  'disable-network-throttling': 'high',
  'timer-resolution-0-5ms': 'high',
  'optimize-network-power': 'high',
  'shadows': 'high',
  'disable-automatic-maintenance': 'high',
  'disable-modern-standby': 'high',
  'disable-telemetry': 'high',
  'disable-search-indexing': 'high',
  'disable-delivery-optimization': 'high',
  'disable-bits': 'high',
  'disable-ceip-tasks': 'high',
  'disable-edge-startup-boost': 'high',
  // Low: QoL, cosmetic or security-side tweaks with negligible FPS effect
  'animations': 'low',
  'transparency': 'low',
  'taskbar-icons': 'low',
  'show-seconds-clock': 'low',
  'hide-meet-now': 'low',
  'delay-taskbar-thumbnails': 'low',
  'never-combine-taskbar-icons': 'low',
  'disable-window-shake': 'low',
  'disable-snap-layouts-flyout': 'low',
  'disable-window-arrange-drag': 'low',
  'disable-balloon-tips': 'low',
  'hide-start-recommended': 'low',
  'hide-copilot-button': 'low',
  'disable-drag-full-window': 'low',
  'menu-delay': 'low',
  'numlock-on-boot': 'low',
  'disable-sticky-keys': 'low',
  'disable-filter-keys': 'low',
  'disable-toggle-keys': 'low',
  'disable-clicklock': 'low',
  'deny-user-account-information': 'low',
  'deny-documents-library': 'low',
  'deny-pictures-library': 'low',
  'deny-videos-library': 'low',
  'deny-email-access': 'low',
  'deny-radios-access': 'low',
  'deny-human-presence': 'low',
  'disable-advertising-id': 'low',
  'disable-tailored-experiences': 'low',
  'disable-windows-feedback': 'low',
  'disable-app-suggestions': 'low',
  'disable-start-tracking': 'low',
  'disable-welcome-experience': 'low',
  'disable-spotlight-wallpapers': 'low',
  'disable-clipboard-history': 'low',
  'disable-clipboard-cloud-sync': 'low',
};

export const riskReasonById: Record<string, { es: string; en: string }> = {
  'dns-optimization': { es: 'Fuerza DNS de Cloudflare y puede ignorar el DNS corporativo, parental o de la VPN.', en: 'Forces Cloudflare DNS and may bypass corporate, parental-control, or VPN DNS.' },
  'disable-print-spooler': { es: 'Impide imprimir hasta volver a activar el servicio Spooler.', en: 'Prevents printing until the Spooler service is re-enabled.' },
  'disable-search-indexing': { es: 'Las búsquedas de archivos serán más lentas porque Windows deja de indexarlos.', en: 'File searches become slower because Windows stops indexing them.' },
  'disable-superfetch': { es: 'Puede empeorar el inicio de aplicaciones en algunos equipos, especialmente con discos mecánicos.', en: 'May worsen application launch times on some systems, especially with hard drives.' },
  'disable-hibernation': { es: 'Desactiva la hibernación y elimina el archivo hiberfil.sys; también afecta Inicio rápido.', en: 'Disables hibernation and removes hiberfil.sys; Fast Startup is also affected.' },
  'disable-bits': { es: 'Puede interrumpir descargas de Windows Update y de aplicaciones que usan BITS.', en: 'May interrupt Windows Update and application downloads that use BITS.' },
  'disable-network-throttling': { es: 'Quita el límite multimedia de red; puede aumentar el uso de CPU y no garantiza menor latencia.', en: 'Removes the multimedia network limit; it may increase CPU use and does not guarantee lower latency.' },
  'optimize-network-power': { es: 'Evita que Windows apague el adaptador para ahorrar energía, aumentando el consumo en portátiles.', en: 'Prevents Windows from powering down the adapter, increasing laptop power use.' },
  'disable-llmnr': { es: 'Desactiva resolución de nombres local; algunos recursos de red antiguos dejarán de resolverse por nombre.', en: 'Disables local name resolution; some older network resources may stop resolving by name.' },
  'remove-onedrive': { es: 'Elimina OneDrive y no puede restaurarse automáticamente; los archivos sincronizados requieren respaldo.', en: 'Removes OneDrive and cannot be automatically restored; synced files require a backup.' },
  'winsock-reset': { es: 'Reinicia el catálogo Winsock y puede desconectar aplicaciones hasta reiniciar Windows.', en: 'Resets the Winsock catalog and may disconnect applications until Windows is restarted.' },
  'flush-dns': { es: 'Borra la caché DNS actual; las siguientes consultas pueden tardar más mientras se reconstruye.', en: 'Clears the current DNS cache; subsequent lookups may be slower while it rebuilds.' },
  'reset-network': { es: 'Reinicia la pila IP/TCP y puede cerrar conexiones activas; reinicia Windows después.', en: 'Resets the IP/TCP stack and may close active connections; restart Windows afterward.' },
  'clear-temp-files': { es: 'Borra archivos temporales y la papelera; no se pueden restaurar automáticamente.', en: 'Deletes temporary files and the recycle bin; they cannot be restored automatically.' },
  'registry-cleanup': { es: 'Elimina historiales MRU y rutas escritas del usuario; no se pueden restaurar automáticamente.', en: 'Deletes user MRU histories and typed paths; they cannot be restored automatically.' },
  'timer-resolution-0-5ms': { es: 'Aumenta el consumo de energía y solo dura mientras la sesión auxiliar está activa.', en: 'Increases power use and lasts only while the helper session is active.' },
  'disable-netbios': { es: 'Equipos muy antiguos que resuelvan nombres por NetBIOS dejarán de verse en la red local.', en: 'Very old machines relying on NetBIOS name resolution will no longer be visible on the LAN.' },
  'disable-smb1': { es: 'Dejarán de funcionar recursos compartidos de archivos o impresoras que solo hablen SMBv1.', en: 'File or printer shares that only speak SMBv1 will stop working.' },
  'disable-fullscreen-optimizations': { es: 'Algunos juegos pueden mostrar parpadeos o cambiar mal de resolución sin las optimizaciones.', en: 'Some games may flicker or mishandle resolution changes without fullscreen optimizations.' },
  'disable-8dot3-names': { es: 'Algunos instaladores o apps muy antiguas pueden fallar si dependen de nombres cortos 8.3.', en: 'Some very old installers or apps may fail if they rely on 8.3 short names.' },
  'disable-admin-shares': { es: 'Las rutas administrativas C$/ADMIN$ dejarán de estar disponibles en red hasta revertir.', en: 'Administrative C$/ADMIN$ network paths stop being available until reverted.' },
  'disable-remote-desktop': { es: 'Bloquea el Escritorio Remoto entrante; no podrás conectarte a este equipo por RDP hasta revertirlo.', en: 'Blocks incoming Remote Desktop; you cannot connect to this PC over RDP until reverted.' },
  'disable-driver-search': { es: 'Windows ya no buscará controladores automáticamente; tendrás que instalarlos manualmente.', en: 'Windows stops searching for drivers automatically; you must install them manually.' },
  'require-network-level-auth': { es: 'Clientes RDP antiguos sin NLA no podrán conectarse a este equipo.', en: 'Older RDP clients without NLA cannot connect to this PC.' },
  'disable-wpad': { es: 'Entornos con proxy automático (WPAD/PAC) pueden perder la configuración de proxy detectada.', en: 'Environments with auto-proxy (WPAD/PAC) may lose detected proxy configuration.' },
  'disable-active-probing': { es: 'El icono de red puede mostrar "sin Internet" aunque la conexión funcione.', en: 'The network icon may show "no internet" even when connectivity works.' },
  'disable-hotspot-service': { es: 'No podrás compartir tu conexión mediante el punto de acceso móvil hasta revertir.', en: 'You cannot share your connection via Mobile Hotspot until reverted.' },
  'disable-ssdp-discovery': { es: 'Descubre menos dispositivos DLNA/UPnP en la red local (Chromecast, altavoces, NAS).', en: 'Fewer DLNA/UPnP devices are discovered on the LAN (Chromecasts, speakers, NAS).' },
  'disable-upnp-device-host': { es: 'Apps que publiquen dispositivos UPnP dejarán de exponerse; puede afectar streaming local.', en: 'Apps exposing UPnP devices stop publishing them; local streaming may be affected.' },
  'disable-tablet-input-service': { es: 'Desactiva el teclado táctil, el panel de escritura y la entrada de lápiz hasta revertir.', en: 'Disables the touch keyboard, handwriting panel, and pen input until reverted.' },
  'disable-window-arrange-drag': { es: 'Arrastrar ventanas a los bordes ya no las ajusta; sigue disponible Win+flechas.', en: 'Dragging windows to edges no longer snaps them; Win+arrows still work.' },
  'hide-start-recommended': { es: 'Oculta la sección Recomendados del Inicio; accesos y archivos recientes dejan de verse ahí.', en: 'Hides the Start Recommended section; recent files and shortcuts disappear from there.' },
  'optimize-thread-scheduling': { es: 'Favorece al proceso en primer plano; tareas en segundo plano pueden tardar más.', en: 'Favors the foreground process; background tasks may take longer.' },
  'disable-svchost-split-threshold': { es: 'Agrupa servicios en procesos compartidos; un fallo de uno puede afectar a sus vecinos.', en: 'Groups services into shared processes; one crash can affect its neighbors.' },
  'optimize-ntfs-memory-usage': { es: 'Aumenta el pool paginado para NTFS: consume más RAM a cambio de metadatos más rápidos.', en: 'Raises the NTFS paged pool: uses more RAM in exchange for faster metadata.' },
  'disable-modern-standby': { es: 'Cambia S0 por sueño clásico tras reiniciar; en algunos portátiles el sueño puede dejar de funcionar.', en: 'Switches S0 to classic sleep after reboot; on some laptops sleep may break entirely.' },
  'disable-automatic-maintenance': { es: 'Defragmentación, diagnósticos y actualizaciones automáticas programadas no se ejecutarán solas.', en: 'Scheduled defrag, diagnostics, and automatic updates will not run on their own.' },
  'disable-app-readiness': { es: 'La preparación de apps Store en primer inicio de sesión queda deshabilitada.', en: 'Store app preparation at first sign-in is disabled.' },
  'disable-ssl-time-seeding': { es: 'Reduce una fuente adicional de sincronización horaria; mantén NTP activo.', en: 'Removes an extra time-sync source; keep NTP enabled.' },
  'deny-user-account-information': { es: 'Apps que lean nombre/cuenta del usuario recibirán acceso denegado.', en: 'Apps reading your account name/info will be denied access.' },
  'deny-documents-library': { es: 'Apps de la tienda no podrán leer tu biblioteca Documentos.', en: 'Store apps can no longer read your Documents library.' },
  'deny-pictures-library': { es: 'Apps de la tienda no podrán leer tu biblioteca Imágenes.', en: 'Store apps can no longer read your Pictures library.' },
  'deny-videos-library': { es: 'Apps de la tienda no podrán leer tu biblioteca Vídeos.', en: 'Store apps can no longer read your Videos library.' },
  'deny-email-access': { es: 'Apps de correo de la tienda perderán el acceso a datos de email del sistema.', en: 'Store mail apps lose access to system email data.' },
  'deny-radios-access': { es: 'Ninguna app podrá activar/desactivar Wi-Fi o Bluetooth por su cuenta.', en: 'No app can toggle Wi-Fi or Bluetooth on its own anymore.' },
  'deny-human-presence': { es: 'Sensores de presencia (wake-on-approach, lock-on-leave) dejarán de alimentar a las apps.', en: 'Presence sensors (wake-on-approach, lock-on-leave) stop feeding apps.' },
  'windowed-games-optimization': { es: 'En pantallas sin HDR o juegos DX10/11 en modo ventana puede no notarse beneficio; revierte si ves parpadeos.', en: 'On non-HDR displays or windowed DX10/11 games the benefit may be imperceptible; revert if you see flickering.' },
  'disable-multiplane-overlay': { es: 'Algunas configuraciones de monitor/GPU prefieren MPO; puede alterar overlays de captura.', en: 'Some monitor/GPU setups prefer MPO; capture overlays may behave differently.' },
  'static-pagefile': { es: 'Fija pagefile de ~1,5x tu RAM en el disco del sistema: ocupa ese espacio fijo y requiere reinicio.', en: 'Fixes a ~1.5x-RAM pagefile on the system drive: reserves that space and requires a reboot.' },
  'uninstall-copilot': { es: 'Elimina la app de Copilot del equipo; algunos usuarios la quieren para productividad asistida.', en: 'Removes the Copilot app; some users want it for assisted productivity.' },
  'uninstall-bing-search': { es: 'Los resultados web del buscador de Windows dejarán de abrirse en su app dedicada.', en: 'Windows search web results will no longer open in the dedicated app.' },
  'deny-broad-filesystem': { es: 'Apps de escritorio con permiso total de archivos perderán ese acceso amplio.', en: 'Desktop apps granted full file access lose that broad permission.' },
  'max-system-responsiveness': { es: 'Prioriza apps en primer plano sobre tareas multimedia de fondo; sube ligeramente el uso de CPU total.', en: 'Favors foreground apps over background multimedia; slightly raises total CPU use.' },
  'disable-memory-integrity': { es: 'REDUCE LA SEGURIDAD: desactiva el aislamiento de kernel (HVCI). Ganas FPS reales, pero un exploit de kernel tendrá más fácil comprometer el sistema. IMPORTANTE: requiere reiniciar ANTES de abrir juegos; aplicarlo y entrar directo a Valorant puede impedir que arranque (Vanguard revalida en el arranque). No lo uses si manejas datos sensibles.', en: 'REDUCES SECURITY: turns off kernel isolation (HVCI). You gain real FPS, but a kernel exploit has an easier time compromising the system. IMPORTANT: reboot BEFORE launching games; applying it and launching Valorant directly may prevent it from starting (Vanguard revalidates at boot). Avoid if you handle sensitive data.' },
};

const warningRiskIds = new Set([
  'disable-telemetry', 'disable-cortana', 'disable-search-indexing', 'disable-superfetch',
  'disable-print-spooler', 'dns-optimization',
  'winsock-reset', 'reset-network', 'disable-network-throttling',
  'optimize-network-power', 'disable-llmnr',
  'disable-usb-suspend', 'disable-hibernation', 'disable-fast-startup', 'enable-hags', 'disable-bits',
  'disable-location-tracking', 'disable-camera-access', 'disable-microphone-access', 'disable-contacts-access',
  'disable-calendar-access', 'disable-speech-recognition', 'disable-handwriting-data', 'remove-onedrive',
  'disable-netbios', 'disable-smb1', 'disable-fullscreen-optimizations', 
  'disable-8dot3-names', 'disable-admin-shares', 'disable-remote-desktop', 'disable-driver-search',
  'require-network-level-auth', 'disable-wpad', 'disable-active-probing', 'disable-hotspot-service',
  'disable-ssdp-discovery', 'disable-upnp-device-host', 'disable-tablet-input-service',
  'disable-window-arrange-drag', 'hide-start-recommended',
  'optimize-thread-scheduling', 'disable-svchost-split-threshold',
  'disable-modern-standby', 'disable-automatic-maintenance',
  'disable-app-readiness', 'disable-ssl-time-seeding',
  'max-system-responsiveness', 'disable-memory-integrity', 'uninstall-copilot', 'uninstall-bing-search',
  'deny-user-account-information', 'deny-documents-library', 'deny-pictures-library', 'deny-videos-library',
  'deny-email-access', 'deny-radios-access', 'deny-human-presence', 'deny-broad-filesystem',
]);

export function getRiskLevel(id: string): 'safe' | 'warning' | 'dangerous' {
  if (irreversibleOptimizationIds.has(id)) return 'dangerous';
  return warningRiskIds.has(id) ? 'warning' : 'safe';
}

export function getRiskReason(id: string): { es: string; en: string } {
  return riskReasonById[id] || (irreversibleOptimizationIds.has(id)
    ? { es: 'No se puede revertir automáticamente. Crea una copia de seguridad y confirma antes de continuar.', en: 'Cannot be reverted automatically. Create a backup and confirm before continuing.' }
    : { es: 'Puede cambiar el comportamiento de Windows y requiere comprobar el resultado antes de usar el equipo normalmente.', en: 'May change Windows behavior and should be verified before normal use.' });
}

export const privacyBenefitById: Record<string, { es: string; en: string }> = {
  'disable-telemetry': { es: 'Reduce la telemetría y el envío de diagnósticos.', en: 'Reduces telemetry and diagnostic data sharing.' },
  'disable-error-reporting': { es: 'Evita el envío automático de informes de errores.', en: 'Prevents automatic error report submission.' },
  'disable-advertising-id': { es: 'Evita el identificador publicitario por usuario.', en: 'Prevents use of the per-user advertising ID.' },
  'disable-tailored-experiences': { es: 'Evita experiencias personalizadas basadas en diagnósticos.', en: 'Prevents experiences tailored from diagnostic data.' },
  'disable-activity-history': { es: 'Evita publicar y subir el historial de actividad.', en: 'Prevents publishing and uploading activity history.' },
  'disable-location-tracking': { es: 'Bloquea el acceso global a la ubicación.', en: 'Blocks global access to device location.' },
  'disable-windows-feedback': { es: 'Reduce solicitudes de comentarios de Windows.', en: 'Reduces Windows feedback prompts.' },
  'disable-cloud-content': { es: 'Bloquea contenido promocional de servicios en la nube.', en: 'Blocks promotional cloud consumer content.' },
  'disable-app-suggestions': { es: 'Reduce sugerencias de aplicaciones en Windows.', en: 'Reduces Windows application suggestions.' },
  'disable-start-tracking': { es: 'Evita el seguimiento de aplicaciones del menú Inicio.', en: 'Prevents Start menu application tracking.' },
  'disable-setting-sync': { es: 'Evita sincronizar preferencias entre dispositivos.', en: 'Prevents syncing preferences between devices.' },
  'disable-input-personalization': { es: 'Evita recopilar datos de escritura y tinta.', en: 'Prevents collection of typing and inking data.' },
  'disable-handwriting-data': { es: 'Evita compartir datos de escritura manual.', en: 'Prevents handwriting data sharing.' },
  'disable-speech-recognition': { es: 'Desactiva el reconocimiento de voz online.', en: 'Disables online speech recognition.' },
  'disable-find-my-device': { es: 'Desactiva la sincronización de ubicación del dispositivo.', en: 'Disables device location synchronization.' },
  'disable-camera-access': { es: 'Bloquea el acceso de aplicaciones a la cámara.', en: 'Blocks application access to the camera.' },
  'disable-microphone-access': { es: 'Bloquea el acceso de aplicaciones al micrófono.', en: 'Blocks application access to the microphone.' },
  'disable-contacts-access': { es: 'Bloquea el acceso de aplicaciones a contactos.', en: 'Blocks application access to contacts.' },
  'disable-calendar-access': { es: 'Bloquea el acceso de aplicaciones al calendario.', en: 'Blocks application access to the calendar.' },
};

export const nonExecutableOptimizationIds = new Set([
  'optimize-startup',
  'mouse-polling',
  'enable-mouse-raw-input',
  'enable-msi-gpu',
]);

export const nonExecutableReasonById: Record<string, { es: string; en: string }> = {
  'optimize-startup': { es: 'Requiere que el usuario elija qué aplicaciones deshabilitar en el Administrador de tareas; no se puede decidir de forma segura automáticamente.', en: 'Requires the user to choose which applications to disable in Task Manager; it cannot be decided safely automatically.' },
  'mouse-polling': { es: 'El polling lo controla el firmware y el software del fabricante del ratón, no Windows de forma global.', en: 'Polling is controlled by mouse firmware and vendor software, not globally by Windows.' },
  'enable-mouse-raw-input': { es: 'Raw Input se solicita desde cada aplicación; Windows no ofrece un interruptor global que esta herramienta pueda aplicar.', en: 'Raw Input is requested by each application; Windows has no global switch this tool can apply.' },
  'enable-msi-gpu': { es: 'El modo MSI de la GPU depende del controlador y del fabricante; forzarlo sin conocer el dispositivo puede impedir que arranque.', en: 'GPU MSI mode depends on the driver and vendor; forcing it without device-specific knowledge can prevent startup.' },
};

const baseVerificationCommands: Record<string, string> = {
  'disable-telemetry': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction Stop).AllowTelemetry -ne 0) { throw 'Telemetry policy was not applied' }; if ((Get-Service DiagTrack -ErrorAction Stop).StartType -ne 'Disabled') { throw 'DiagTrack was not disabled' }",
  'disable-cortana': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop).AllowCortana -ne 0) { throw 'Cortana policy was not applied' }",
  'disable-search-indexing': "if ((Get-Service WSearch -ErrorAction Stop).StartType -ne 'Disabled') { throw 'Windows Search was not disabled' }",
  'disable-superfetch': "if ((Get-Service SysMain -ErrorAction Stop).StartType -ne 'Disabled') { throw 'SysMain was not disabled' }",
  'disable-print-spooler': "if ((Get-Service Spooler -ErrorAction Stop).StartType -ne 'Disabled') { throw 'Print Spooler was not disabled' }",
  'disable-xbox-gamebar': "$gameDvr = Get-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR' -Name AppCaptureEnabled -ErrorAction Stop; if ($gameDvr.AppCaptureEnabled -ne 0) { throw 'Game DVR was not disabled' }",
  'dns-optimization': "$bad = Get-NetAdapter -Physical -Status Up -ErrorAction Stop | Where-Object { $servers = @(Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction Stop).ServerAddresses; -not ($servers -contains '1.1.1.1' -and $servers -contains '1.0.0.1') }; if ($bad) { throw 'Cloudflare DNS was not applied to every active adapter' }",
  'clear-temp-files': "if (-not (Test-Path $env:TEMP)) { throw 'User temporary directory is unavailable after cleanup' }; if (-not (Test-Path 'C:\\Windows\\Temp')) { throw 'Windows temporary directory is unavailable after cleanup' }; Write-Output 'Temporary directories remain accessible after cleanup'",
  'registry-cleanup': "$mru = Get-Item 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RunMRU' -ErrorAction SilentlyContinue; if ($mru -and (Get-ItemProperty $mru.PSPath | Get-Member -MemberType NoteProperty | Where-Object Name -ne 'PSPath')) { throw 'RunMRU entries remain after cleanup' }; Write-Output 'MRU history cleanup verified'",
  'timer-resolution-0-5ms': "if (Test-Path (Join-Path $env:TEMP 'ca-o-timer-resolution.pid')) { throw 'Timer resolution helper remains active' }",
  'flush-dns': "Clear-DnsClientCache -ErrorAction Stop; if (@(Get-DnsClientCache -ErrorAction Stop).Count -gt 0) { throw 'DNS cache was not cleared' }",
  'winsock-reset': "$catalog = netsh winsock show catalog; if ($LASTEXITCODE -ne 0 -or -not $catalog) { throw 'Winsock catalog is unavailable after reset' }",
  'reset-network': "if (-not (Get-NetAdapter -Physical -Status Up -ErrorAction Stop)) { throw 'No active physical network adapter after reset' }; if (-not ((netsh int tcp show global | Out-String) -match '(?i)receive|recepción')) { throw 'TCP stack is unavailable after reset' }",
  'mouse-acceleration': "$mouse = Get-ItemProperty 'HKCU:\\Control Panel\\Mouse' -ErrorAction Stop; if ($mouse.MouseSpeed -ne '0' -or $mouse.MouseThreshold1 -ne '0' -or $mouse.MouseThreshold2 -ne '0') { throw 'Mouse acceleration was not disabled' }",
  'keyboard-rate': "$keyboard = Get-ItemProperty 'HKCU:\\Control Panel\\Keyboard' -ErrorAction Stop; if ($keyboard.KeyboardDelay -ne '0' -or $keyboard.KeyboardSpeed -ne '31') { throw 'Keyboard settings were not applied' }",
  'touchpad-latency': "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name AAPThreshold -ErrorAction Stop).AAPThreshold -ne 0) { throw 'Touchpad latency setting was not applied' }",
  animations: "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop\\WindowMetrics' -Name MinAnimate -ErrorAction Stop).MinAnimate -ne '0') { throw 'Animations were not disabled' }",
  transparency: "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name EnableTransparency -ErrorAction Stop).EnableTransparency -ne 0) { throw 'Transparency was not disabled' }",
  shadows: "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name VisualFXSetting -ErrorAction Stop).VisualFXSetting -ne 2) { throw 'Visual effects were not optimized' }",
  'taskbar-icons': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarAl -ErrorAction Stop).TaskbarAl -ne 0) { throw 'Taskbar alignment was not applied' }",
  notifications: "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Name DisableNotificationCenter -ErrorAction Stop).DisableNotificationCenter -ne 1) { throw 'Notifications were not disabled' }",
  'power-plan': "$guidFile = Join-Path $env:TEMP 'ca-o-powerplan.guid'; $stashed = if (Test-Path $guidFile) { (Get-Content $guidFile -ErrorAction Stop | Select-Object -First 1).Trim() } else { '' }; $active = powercfg /getactivescheme | Out-String; if ($stashed -and $active -match $stashed) { Write-Output \"Stashed Ultimate Performance plan GUID is active ($stashed)\" } elseif ($active -match '(?i)ultimate') { Remove-Item $guidFile -Force -ErrorAction SilentlyContinue; Write-Output 'Ultimate Performance plan detected as active' } else { throw 'Ultimate Performance plan is not active' }",
  'gaming-mode': "$gameBar = Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\GameBar' -ErrorAction Stop; if ($gameBar.AllowAutoGameMode -ne 1 -or $gameBar.AutoGameModeEnabled -ne 1) { throw 'Game Mode was not enabled' }",
  'disable-services': "foreach ($name in @('MapsBroker','Fax')) { $service = Get-Service -Name $name -ErrorAction SilentlyContinue; if ($service -and $service.StartType -ne 'Disabled') { throw \"Optional service $name was not disabled\" } }; Write-Output 'Available optional services verified'",
  'memory-compression': "if ((Get-MMAgent -ErrorAction Stop).MemoryCompression) { throw 'Memory compression is still enabled' }"
};

export const verificationCommands = {
  ...baseVerificationCommands,
  'disable-error-reporting': "if ((Get-Service WerSvc -ErrorAction Stop).StartType -ne 'Disabled') { throw 'WER was not disabled' }",
  'disable-delivery-optimization': "if ((Get-Service DoSvc -ErrorAction Stop).StartType -ne 'Disabled') { throw 'Delivery Optimization was not disabled' }",
  'disable-windows-insider': "if (Get-Service wisvc -ErrorAction SilentlyContinue) { if ((Get-Service wisvc).StartType -ne 'Disabled') { throw 'Windows Insider service was not disabled' } }",
  'disable-retail-demo': "if (Get-Service RetailDemo -ErrorAction SilentlyContinue) { if ((Get-Service RetailDemo).StartType -ne 'Disabled') { throw 'Retail Demo service was not disabled' } }",
  'disable-network-throttling': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name NetworkThrottlingIndex -ErrorAction Stop).NetworkThrottlingIndex -ne 4294967295) { throw 'Network throttling was not disabled' }",
  'optimize-network-power': "if (-not (Get-NetAdapter -Physical -ErrorAction Stop)) { throw 'No physical network adapter found' }",
  'enable-mouse-raw-input': "Write-Output 'Raw input is requested per application by each game; there is no global Windows value to verify.'",
  'disable-lock-screen': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction Stop).NoLockScreen -ne 1) { throw 'Lock screen was not disabled' }",
  'disable-aero-peek': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop).DisablePreviewDesktop -ne 1) { throw 'Aero Peek was not disabled' }",
  'disable-startup-sound': "if ((Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop).'(Default)' -ne '') { throw 'Startup sound was not disabled' }",
  'disable-cast-notifications': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction Stop).AllowWhileLocked -ne 0) { throw 'Cast notifications were not disabled' }",
  'disable-background-apps': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction Stop).GlobalUserDisabled -ne 1) { throw 'Background apps were not disabled' }",
  'disable-start-menu-suggestions': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop).SystemPaneSuggestionsEnabled -ne 0) { throw 'Start suggestions were not disabled' }",
  'disable-game-dvr': "if ((Get-ItemProperty 'HKCU:\\System\\GameConfigStore' -Name GameDVR_Enabled -ErrorAction Stop).GameDVR_Enabled -ne 0) { throw 'Game DVR was not disabled' }",
  'disable-power-throttling': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling' -Name PowerThrottlingOff -ErrorAction Stop).PowerThrottlingOff -ne 1) { throw 'Power throttling was not disabled' }",
  'enable-msi-gpu': "if (-not (Get-PnpDevice -Class Display -Status OK -ErrorAction Stop)) { throw 'No active display device found' }",
  'disable-cpu-idle': "if ((powercfg /query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN | Out-String) -notmatch '0x00000064') { throw 'Minimum processor state was not set to 100 percent' }",
  'disable-core-parking': "if ((powercfg /query SCHEME_CURRENT SUB_PROCESSOR CPMINCORES | Out-String) -notmatch '0x00000064') { throw 'Core parking was not disabled' }",
  'disable-memory-dumps': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\CrashControl' -Name CrashDumpEnabled -ErrorAction Stop).CrashDumpEnabled -ne 0) { throw 'Memory dumps were not disabled' }",
  'enable-long-paths': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name LongPathsEnabled -ErrorAction Stop).LongPathsEnabled -ne 1) { throw 'Long paths were not enabled' }",
  'timer-resolution-0-5ms': "if (-not (Test-Path (Join-Path $env:TEMP 'ca-o-timer-resolution.pid'))) { throw 'Timer resolution helper is not running' }; if (-not ('CAOTimerQuery.NativeMethods' -as [type])) { Add-Type @'\nusing System;\nusing System.Runtime.InteropServices;\npublic static class CAOTimerQueryNativeMethods {\n  [DllImport(\"ntdll.dll\")] public static extern uint NtQueryTimerResolution(ref uint MaximumResolution, ref uint MinimumResolution, ref uint CurrentResolution);\n}\n'@ }; [uint32]$max=0; [uint32]$min=0; [uint32]$current=0; [void][CAOTimerQueryNativeMethods]::NtQueryTimerResolution([ref]$max,[ref]$min,[ref]$current); if ($current -gt 5000) { throw \"Current timer resolution is $current (target 5000 = 0.5 ms)\" }",
  'remove-onedrive': "if (Get-Process OneDrive -ErrorAction SilentlyContinue) { throw 'OneDrive is still running' }; $paths=@(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\",\"$env:ProgramFiles\\Microsoft OneDrive\\OneDrive.exe\"); if ($paths | Where-Object { Test-Path $_ }) { throw 'OneDrive executable is still present' }",
  'disable-advertising-id': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop).Enabled -ne 0) { throw 'Advertising ID is still enabled' }",
  'disable-tailored-experiences': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop).TailoredExperiencesWithDiagnosticDataEnabled -ne 0) { throw 'Tailored experiences remain enabled' }",
  'disable-activity-history': "$p=Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -ErrorAction Stop; if ($p.PublishUserActivities -ne 0 -or $p.UploadUserActivities -ne 0) { throw 'Activity history policies were not disabled' }",
  'disable-location-tracking': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Location tracking is not denied' }",
  'disable-windows-feedback': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction Stop).NumberOfSIUFInPeriod -ne 0) { throw 'Windows feedback remains enabled' }",
  'disable-cloud-content': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction Stop).DisableConsumerAccountStateContent -ne 1) { throw 'Cloud consumer content was not disabled' }",
  'disable-app-suggestions': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop).'SubscribedContent-338388Enabled' -ne 0) { throw 'App suggestions were not disabled' }",
  'disable-start-tracking': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop).Start_TrackProgs -ne 0) { throw 'Start tracking was not disabled' }",
  'disable-setting-sync': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction Stop).SyncPolicy -ne 5) { throw 'Settings sync was not disabled' }",
  'disable-input-personalization': "$p=Get-ItemProperty 'HKCU:\\Software\\Microsoft\\InputPersonalization' -ErrorAction Stop; if ($p.RestrictImplicitInkCollection -ne 1 -or $p.RestrictImplicitTextCollection -ne 1) { throw 'Input personalization was not disabled' }",
  'disable-handwriting-data': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction Stop).PreventHandwritingDataSharing -ne 1) { throw 'Handwriting data sharing was not disabled' }",
  'disable-speech-recognition': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop).HasAccepted -ne 0) { throw 'Online speech recognition was not disabled' }",
  'disable-find-my-device': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop).LocationSyncEnabled -ne 0) { throw 'Find My Device was not disabled' }",
  'disable-contacts-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\contacts' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Contacts access was not denied' }",
  'disable-calendar-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appointments' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Calendar access was not denied' }",
  'disable-camera-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Camera access was not denied' }",
  'disable-microphone-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Microphone access was not denied' }",
  'disable-llmnr': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient' -Name EnableMulticast -ErrorAction Stop).EnableMulticast -ne 0) { throw 'LLMNR was not disabled' }",
  'menu-delay': "if ((Get-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MenuShowDelay -ErrorAction Stop).MenuShowDelay -ne '0') { throw 'Menu delay was not changed' }",
  'inactive-window-scroll': "if ((Get-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MouseWheelRouting -ErrorAction Stop).MouseWheelRouting -ne 0) { throw 'Inactive window scrolling was not disabled' }",
  'disable-sticky-keys': "if ((Get-ItemProperty -Path 'HKCU:\\Control Panel\\Accessibility\\StickyKeys' -Name Flags -ErrorAction Stop).Flags -ne '506') { throw 'Sticky Keys shortcut was not disabled' }",
  'disable-usb-suspend': "if ((powercfg /query SCHEME_CURRENT SUB_USB USBSELECTIVE | Out-String) -match '0x00000001') { throw 'USB selective suspend remains enabled' }",
  'show-file-extensions': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop).HideFileExt -ne 0) { throw 'File extensions are still hidden' }",
  'disable-thumbnails': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop).IconsOnly -ne 1) { throw 'Thumbnails were not disabled' }",
  'disable-tooltips': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop).ShowInfoTip -ne 0) { throw 'Tooltips were not disabled' }",
  'disable-wallpaper-slideshow': "if ((Get-ItemProperty -Path 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop).Interval -ne 0) { throw 'Wallpaper slideshow was not disabled' }",
  'disable-system-sounds': "if ((Get-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -ErrorAction Stop).'(Default)' -ne '.None') { throw 'System sounds were not disabled' }",
  'show-hidden-files': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop).Hidden -ne 1) { throw 'Hidden files are not shown' }",
  'hide-task-view': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop).ShowTaskViewButton -ne 0) { throw 'Task View button was not hidden' }",
  'disable-taskbar-search': "if ((Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop).SearchboxTaskbarMode -ne 0) { throw 'Taskbar search was not reduced' }",
  'disable-hibernation': "$power = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -ErrorAction Stop; $hibernateEnabled = if ($power.PSObject.Properties.Name -contains 'HibernateEnabled') { $power.HibernateEnabled } elseif ($power.PSObject.Properties.Name -contains 'HibernateEnabledDefault') { $power.HibernateEnabledDefault } else { 1 }; if ($hibernateEnabled -ne 0) { throw 'Hibernation is still enabled' }",
  'disable-fast-startup': "if ((Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled -ErrorAction Stop).HiberbootEnabled -ne 0) { throw 'Fast Startup was not disabled' }",
  'enable-hags': "if ((Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' -Name HwSchMode -ErrorAction Stop).HwSchMode -ne 2) { throw 'HAGS was not enabled' }",
  'disable-bits': "if ((Get-Service BITS -ErrorAction Stop).StartType -ne 'Disabled') { throw 'BITS was not disabled' }",
  'max-system-responsiveness': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name SystemResponsiveness -ErrorAction Stop).SystemResponsiveness -ne 0) { throw 'System responsiveness was not maximized' }",
  'disable-memory-integrity': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity' -Name Enabled -ErrorAction Stop).Enabled -ne 0) { throw 'Memory Integrity was not disabled' }",
  'uninstall-copilot': "if (Get-AppxPackage -Name '*Microsoft.Windows.Ai.Copilot*' -ErrorAction SilentlyContinue) { throw 'Copilot remains installed' }; Write-Output 'Copilot verified removed'",
  'uninstall-bing-search': "if (Get-AppxPackage -Name '*Microsoft.BingSearch*' -ErrorAction SilentlyContinue) { throw 'Bing Search remains installed' }; Write-Output 'Bing Search verified removed'",
  'disable-click-to-do': "$ctdUser = (Get-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableClickToDo -ErrorAction Stop).DisableClickToDo; $ctdMachine = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableClickToDo -ErrorAction Stop).DisableClickToDo; if ($ctdUser -ne 1 -or $ctdMachine -ne 1) { throw 'Click to Do was not disabled' }",
  'disable-paint-ai': "$paintCheck = Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Paint' -ErrorAction Stop; if ($paintCheck.DisableCocreator -ne 1 -or $paintCheck.DisableImageGenerator -ne 1) { throw 'Paint AI features were not disabled' }",
  'static-pagefile': "$pf = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management' -Name PagingFiles -ErrorAction Stop).PagingFiles[0]; if ($pf -notmatch 'pagefile\\.sys \\d+ \\d+$') { throw 'Static pagefile was not configured' }",
  'windowed-games-optimization': "$wgo = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences' -Name DirectXUserGlobalSettings -ErrorAction Stop).DirectXUserGlobalSettings; if ($wgo -notmatch 'SwapEffectUpgradeEnable=1') { throw 'Windowed games optimization was not enabled' }",
  'disable-multiplane-overlay': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm' -Name OverlayTestMode -ErrorAction Stop).OverlayTestMode -ne 5) { throw 'Multi-Plane Overlay was not disabled' }",

  // ─── NEW BATCH (2026-08) ──────────────────────────────────────────────
  'disable-widgets': "$widgets = Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Dsh' -Name AllowNewsAndInterests -ErrorAction Stop; if ($widgets.AllowNewsAndInterests -ne 0) { throw 'Widgets policy was not applied' }",
  'disable-recall': "$recall = Get-ItemProperty 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableAIDataAnalysis -ErrorAction Stop; if ($recall.DisableAIDataAnalysis -ne 1) { throw 'Recall policy was not applied' }",
  'disable-netbios': "$badNetbios = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NetBT\\Parameters\\Interfaces' | Where-Object { (Get-ItemProperty $_.PSPath -Name NetbiosOptions -ErrorAction Stop).NetbiosOptions -ne 2 }; if ($badNetbios) { throw 'NetBIOS was not disabled on every interface' }",
  'disable-smb1': "if ((Get-SmbServerConfiguration -ErrorAction Stop).EnableSMB1Protocol) { throw 'SMBv1 remains enabled' }",
  'show-seconds-clock': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowSecondsInSystemClock -ErrorAction Stop).ShowSecondsInSystemClock -ne 1) { throw 'Seconds clock was not enabled' }",
  'hide-meet-now': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowMeetNow -ErrorAction Stop).ShowMeetNow -ne 0) { throw 'Meet Now icon was not hidden' }",
  'disable-fullscreen-optimizations': "if ((Get-ItemProperty 'HKCU:\\System\\GameConfigStore' -Name GameDVR_FSOBehavior -ErrorAction Stop).GameDVR_FSOBehavior -ne 1) { throw 'Fullscreen optimizations were not disabled' }",
  'disable-welcome-experience': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -ErrorAction Stop).'SubscribedContent-310093Enabled' -ne 0) { throw 'Welcome experience was not disabled' }",

  // ─── CATEGORY BATCH x10 EACH (2026-08) ────────────────────────────────
  'disable-ceip-tasks': "$ceipTasks = @(@('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','Consolidator'), @('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','UsbCeip'), @('\\Microsoft\\Windows\\Application Experience\\','Microsoft Compatibility Appraiser'), @('\\Microsoft\\Windows\\Application Experience\\','ProgramDataUpdater')); foreach ($entry in $ceipTasks) { $task = Get-ScheduledTask -TaskPath $entry[0] -TaskName $entry[1] -ErrorAction SilentlyContinue; if ($task -and $task.State -ne 'Disabled') { throw \"Scheduled task $($entry[1]) was not disabled\" } }; Write-Output 'CEIP scheduled tasks verified'",
  'disable-last-access-time': "if (-not ((fsutil behavior query disablelastaccess | Out-String) -match '(?i)DisableLastAccess\\s*=\\s*1')) { throw 'Last-access updates were not disabled' }",
  'disable-8dot3-names': "if (-not ((fsutil behavior query disable8dot3 | Out-String) -match '(?i)Disable8dot3\\s*=\\s*1')) { throw '8.3 name creation was not disabled' }",
  'disable-admin-shares': "$shares = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters' -ErrorAction Stop; if ($shares.AutoShareServer -ne 0 -or $shares.AutoShareWks -ne 0) { throw 'Administrative share policies were not applied' }",
  'disable-remote-assistance': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance' -Name fAllowToGetHelp -ErrorAction Stop).fAllowToGetHelp -ne 0) { throw 'Remote Assistance was not disabled' }",
  'disable-remote-desktop': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server' -Name fDenyTSConnections -ErrorAction Stop).fDenyTSConnections -ne 1) { throw 'Remote Desktop was not blocked' }",
  'speedup-shutdown': "$ctrl = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name WaitToKillServiceTimeout -ErrorAction Stop; $desk = Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name AutoEndTasks -ErrorAction Stop; if ($ctrl.WaitToKillServiceTimeout -ne '2000' -or $desk.AutoEndTasks -ne '1') { throw 'Shutdown speedup values were not applied' }",
  'no-auto-reboot-active': "$noReboot = Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU' -Name NoAutoRebootWithLoggedOnUsers -ErrorAction Stop; if ($noReboot.NoAutoRebootWithLoggedOnUsers -ne 1) { throw 'No-reboot policy was not applied' }",
  'disable-driver-search': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching' -Name SearchOrderConfig -ErrorAction Stop).SearchOrderConfig -ne 0) { throw 'Driver search was not disabled' }",

  'flush-arp-cache': "$probe = arp -a; if ($LASTEXITCODE -ne 0 -or -not $probe) { throw 'ARP table is unavailable after flush' }",
  'disable-hotspot-service': "if (Get-Service -Name icssvc -ErrorAction SilentlyContinue) { if ((Get-Service -Name icssvc).StartType -ne 'Disabled') { throw 'Mobile Hotspot service was not disabled' } }; Write-Output 'Hotspot service verified'",
  'require-network-level-auth': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp' -Name UserAuthentication -ErrorAction Stop).UserAuthentication -ne 1) { throw 'NLA requirement was not applied' }",
  'disable-wpad': "if (Get-Service -Name WinHttpAutoProxySvc -ErrorAction SilentlyContinue) { if ((Get-Service -Name WinHttpAutoProxySvc).StartType -ne 'Disabled') { throw 'WPAD service was not disabled' } }; Write-Output 'WPAD service verified'",
  'disable-active-probing': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NlaSvc\\Parameters\\Internet' -Name EnableActiveProbing -ErrorAction Stop).EnableActiveProbing -ne 0) { throw 'Active probing was not disabled' }",
  'disable-peer-name-resolution': "if (Get-Service -Name PNRPsvc -ErrorAction SilentlyContinue) { if ((Get-Service -Name PNRPsvc).StartType -ne 'Disabled') { throw 'PNRP service was not disabled' } }; Write-Output 'PNRP service verified'",
  'restrict-point-and-print': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\Printers\\PointAndPrint' -Name RestrictDriverInstallationToAdministrators -ErrorAction Stop).RestrictDriverInstallationToAdministrators -ne 1) { throw 'Point and Print restriction was not applied' }",
  'disable-ssdp-discovery': "if (Get-Service -Name SSDPSRV -ErrorAction SilentlyContinue) { if ((Get-Service -Name SSDPSRV).StartType -ne 'Disabled') { throw 'SSDP discovery was not disabled' } }; Write-Output 'SSDP service verified'",
  'disable-upnp-device-host': "if (Get-Service -Name upnphost -ErrorAction SilentlyContinue) { if ((Get-Service -Name upnphost).StartType -ne 'Disabled') { throw 'UPnP Device Host was not disabled' } }; Write-Output 'UPnP host verified'",
  'disable-snmp-trap': "if (Get-Service -Name SNMPTRAP -ErrorAction SilentlyContinue) { if ((Get-Service -Name SNMPTRAP).StartType -ne 'Disabled') { throw 'SNMP Trap was not disabled' } }; Write-Output 'SNMP Trap verified'",

  'disable-filter-keys': "$fk = Get-ItemProperty 'HKCU:\\Control Panel\\Accessibility\\FilterKeys' -Name Flags -ErrorAction Stop; if ($fk.Flags -ne '122') { throw 'Filter Keys shortcut was not disabled' }",
  'disable-toggle-keys': "$tk = Get-ItemProperty 'HKCU:\\Control Panel\\Accessibility\\ToggleKeys' -Name Flags -ErrorAction Stop; if ($tk.Flags -ne '58') { throw 'Toggle Keys shortcut was not disabled' }",
  'disable-touch-keyboard-autoinvoke': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\TabletTip\\1.7' -Name EnableDesktopModeAutoInvoke -ErrorAction Stop).EnableDesktopModeAutoInvoke -ne 0) { throw 'Touch keyboard auto-invoke was not disabled' }",
  'disable-controller-gamebar-chord': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\GameBar' -Name UseNexusForGameBarEnabled -ErrorAction Stop).UseNexusForGameBarEnabled -ne 0) { throw 'Controller Game Bar chord was not disabled' }",
  'disable-touchpad-edge-swipes': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name EdgeSwipeEnabled -ErrorAction Stop).EdgeSwipeEnabled -ne 0) { throw 'Touchpad edge swipes were not disabled' }",
  'disable-touchpad-threefinger-slide': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name ThreeFingerSlideEnabled -ErrorAction Stop).ThreeFingerSlideEnabled -ne 0) { throw 'Three-finger slide was not disabled' }",
  'disable-windows-ink': "$inkPolicy = Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsInkWorkspace' -Name AllowWindowsInkWorkspace -ErrorAction Stop; if ($inkPolicy.AllowWindowsInkWorkspace -ne 0) { throw 'Windows Ink policy was not applied' }",
  'numlock-on-boot': "$numUser = (Get-ItemProperty 'HKCU:\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -ErrorAction Stop).InitialKeyboardIndicators; $numDefault = (Get-ItemProperty 'Registry::HKEY_USERS\\.DEFAULT\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -ErrorAction Stop).InitialKeyboardIndicators; if ($numUser -ne '2' -or $numDefault -ne '2') { throw 'NumLock boot preference was not set' }",
  'disable-hover-checkboxes': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name AutoCheckSelect -ErrorAction Stop).AutoCheckSelect -ne 0) { throw 'Hover checkboxes were not disabled' }",
  'disable-tablet-input-service': "if (Get-Service -Name TabletInputService -ErrorAction SilentlyContinue) { if ((Get-Service -Name TabletInputService).StartType -ne 'Disabled') { throw 'Tablet input service was not disabled' } }; Write-Output 'Tablet input verified'",

  'delay-taskbar-thumbnails': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ExtendedUIHoverTime -ErrorAction Stop).ExtendedUIHoverTime -ne 30000) { throw 'Thumbnail delay was not applied' }",
  'hide-start-recommended': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer' -Name HideRecommendedSection -ErrorAction Stop).HideRecommendedSection -ne 1) { throw 'Recommended section was not hidden' }",
  'disable-drag-full-window': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name DragFullWindows -ErrorAction Stop).DragFullWindows -ne '0') { throw 'Drag full window setting was not applied' }",
  'never-combine-taskbar-icons': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarGlomLevel -ErrorAction Stop).TaskbarGlomLevel -ne 2) { throw 'Taskbar grouping was not changed' }",
  'disable-window-shake': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisableViewShake -ErrorAction Stop).DisableViewShake -ne 1) { throw 'Window shake was not disabled' }",
  'disable-snap-layouts-flyout': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name EnableSnapAssistFlyout -ErrorAction Stop).EnableSnapAssistFlyout -ne 0) { throw 'Snap layouts flyout was not disabled' }",
  'disable-window-arrange-drag': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name WindowArrangementActive -ErrorAction Stop).WindowArrangementActive -ne '0') { throw 'Window arrangement was not disabled' }",
  'disable-spotlight-wallpapers': "$spotlight = Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -ErrorAction Stop; if ($spotlight.RotatingLockScreenEnabled -ne 0 -or $spotlight.RotatingLockScreenOverlayEnabled -ne 0) { throw 'Spotlight lock screen was not switched off' }",
  'hide-copilot-button': "if ((Get-ItemProperty 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot' -Name TurnOffWindowsCopilot -ErrorAction Stop).TurnOffWindowsCopilot -ne 1) { throw 'Copilot button was not hidden' }",

  'optimize-thread-scheduling': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl' -Name Win32PrioritySeparation -ErrorAction Stop).Win32PrioritySeparation -ne 38) { throw 'Thread scheduling was not optimized' }",
  'disable-svchost-split-threshold': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name SvcHostSplitThresholdInKB -ErrorAction Stop).SvcHostSplitThresholdInKB -ne 4294967295) { throw 'svchost split threshold was not changed' }",
  'optimize-ntfs-memory-usage': "if (-not ((fsutil behavior query memoryusage | Out-String) -match '(?i)MemoryUsage\\s*=\\s*2')) { throw 'NTFS memory usage was not raised' }",
  'disable-modern-standby': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name PlatformAoAcOverride -ErrorAction Stop).PlatformAoAcOverride -ne 0) { throw 'Modern Standby was not disabled' }",
  'disable-edge-startup-boost': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge' -Name StartupBoostEnabled -ErrorAction Stop).StartupBoostEnabled -ne 0) { throw 'Edge startup boost was not disabled' }",
  'disable-automatic-maintenance': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Schedule\\Maintenance' -Name MaintenanceDisabled -ErrorAction Stop).MaintenanceDisabled -ne 1) { throw 'Automatic maintenance was not disabled' }",
  'disable-app-readiness': "if (Get-Service -Name AppReadiness -ErrorAction SilentlyContinue) { if ((Get-Service -Name AppReadiness).StartType -ne 'Disabled') { throw 'App Readiness was not disabled' } }; Write-Output 'App Readiness verified'",
  'disable-ssl-time-seeding': "$sslTime = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\W32Time\\Config' -Name UtilizeSslTimeData -ErrorAction Stop; if ($sslTime.UtilizeSslTimeData -ne 0) { throw 'SSL time seeding was not disabled' }",

  'disable-clipboard-history': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Clipboard' -Name EnableClipboardHistory -ErrorAction Stop).EnableClipboardHistory -ne 0) { throw 'Clipboard history was not disabled' }",
  'disable-clipboard-cloud-sync': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Clipboard' -Name CloudClipboardAutomaticUpload -ErrorAction Stop).CloudClipboardAutomaticUpload -ne 0) { throw 'Cloud clipboard sync was not blocked' }",
  'deny-user-account-information': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\userAccountInformation' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Account information access was not denied' }",
  'deny-documents-library': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\documentsLibrary' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Documents access was not denied' }",
  'deny-pictures-library': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\picturesLibrary' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Pictures access was not denied' }",
  'deny-videos-library': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\videosLibrary' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Videos access was not denied' }",
  'deny-email-access': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\email' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Email access was not denied' }",
  'deny-radios-access': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\radios' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Radios access was not denied' }",
  'deny-human-presence': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\humanPresence' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Presence sensor access was not denied' }",
  'deny-broad-filesystem': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\broadFileSystemAccess' -Name Value -ErrorAction Stop).Value -ne 'Deny') { throw 'Broad filesystem access was not denied' }",
};

export const revertVerificationCommands: Record<string, string> = {
  'disable-telemetry': "if ((Get-Service DiagTrack -ErrorAction Stop).StartType -eq 'Disabled') { throw 'DiagTrack remains disabled' }",
  'disable-cortana': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop).AllowCortana -ne 1) { throw 'Cortana policy remains disabled' }",
  'disable-search-indexing': "if ((Get-Service WSearch -ErrorAction Stop).StartType -eq 'Disabled') { throw 'Windows Search remains disabled' }",
  'disable-superfetch': "if ((Get-Service SysMain -ErrorAction Stop).StartType -eq 'Disabled') { throw 'SysMain remains disabled' }",
  'disable-print-spooler': "if ((Get-Service Spooler -ErrorAction Stop).StartType -eq 'Disabled') { throw 'Print Spooler remains disabled' }",
  'disable-xbox-gamebar': "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR' -Name AppCaptureEnabled -ErrorAction Stop).AppCaptureEnabled -ne 1) { throw 'Game DVR remains disabled' }",
  'mouse-acceleration': "$mouse = Get-ItemProperty 'HKCU:\\Control Panel\\Mouse' -ErrorAction Stop; if ($mouse.MouseSpeed -eq '0' -and $mouse.MouseThreshold1 -eq '0' -and $mouse.MouseThreshold2 -eq '0') { throw 'Mouse acceleration remains disabled' }",
  'transparency': "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name EnableTransparency -ErrorAction Stop).EnableTransparency -eq 0) { throw 'Transparency remains disabled' }",
  'power-plan': "$guidFile = Join-Path $env:TEMP 'ca-o-powerplan.guid'; $stashed = if (Test-Path $guidFile) { (Get-Content $guidFile -ErrorAction Stop | Select-Object -First 1).Trim() } else { '' }; $active = powercfg /getactivescheme | Out-String; if ($stashed -and $active -match $stashed) { throw \"Duplicated Ultimate Performance plan remains active ($stashed)\" }; if ($active -match '(?i)ultimate') { throw 'Ultimate Performance plan remains active' }; Remove-Item $guidFile -Force -ErrorAction SilentlyContinue",
  'gaming-mode': "$gameBar = Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\GameBar' -ErrorAction Stop; if ($gameBar.AllowAutoGameMode -eq 1 -or $gameBar.AutoGameModeEnabled -eq 1) { throw 'Game Mode remains enabled' }",
  'memory-compression': "if ((Get-MMAgent -ErrorAction Stop).MemoryCompression -ne $true) { throw 'Memory compression remains disabled' }",
  'disable-services': "foreach ($name in @('MapsBroker','Fax')) { $service = Get-Service -Name $name -ErrorAction SilentlyContinue; if ($service -and $service.StartType -eq 'Disabled') { throw \"Optional service $name remains disabled\" } }; Write-Output 'Optional services verified'",
  'disable-startup-sound': "if ((Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop).'(Default)' -eq '') { throw 'Startup sound remains disabled' }",
  'disable-game-dvr': "if ((Get-ItemProperty 'HKCU:\\System\\GameConfigStore' -Name GameDVR_Enabled -ErrorAction Stop).GameDVR_Enabled -eq 0) { throw 'Game DVR remains disabled' }",
  'hide-task-view': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop).ShowTaskViewButton -eq 0) { throw 'Task View button remains hidden' }",
  'disable-aero-peek': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop).DisablePreviewDesktop -eq 1) { throw 'Aero Peek remains disabled' }",
  'disable-tooltips': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop).ShowInfoTip -eq 0) { throw 'Explorer tooltips remain disabled' }",
  'disable-wallpaper-slideshow': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop).Interval -eq 0) { throw 'Wallpaper slideshow remains disabled' }",
  'disable-system-sounds': "if ((Get-ItemProperty 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -ErrorAction Stop).'(Default)' -eq '.None') { throw 'System sounds remain disabled' }",
  'show-hidden-files': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop).Hidden -eq 1) { throw 'Hidden files remain visible' }",
  'disable-start-menu-suggestions': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop).SystemPaneSuggestionsEnabled -eq 0) { throw 'Start menu suggestions remain disabled' }",
  'disable-taskbar-search': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop).SearchboxTaskbarMode -eq 0) { throw 'Taskbar search remains reduced' }",
  'show-file-extensions': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop).HideFileExt -eq 0) { throw 'File extensions remain visible' }",
  'disable-background-apps': "$globalUserDisabled = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction SilentlyContinue).GlobalUserDisabled; if ($globalUserDisabled -eq 1) { throw 'Background apps remain disabled' }; Write-Output 'Background apps policy restored'",
  'disable-cast-notifications': "$allowWhileLocked = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction SilentlyContinue).AllowWhileLocked; if ($allowWhileLocked -eq 0) { throw 'Cast notifications remain disabled' }; Write-Output 'Cast notifications restored'",
  'disable-thumbnails': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop).IconsOnly -eq 1) { throw 'File thumbnails remain disabled' }",
  'disable-lock-screen': "$noLockScreen = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction SilentlyContinue).NoLockScreen; if ($noLockScreen -eq 1) { throw 'Lock screen remains disabled' }; Write-Output 'Lock screen policy restored'",
  'disable-advertising-id': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop).Enabled -eq 0) { throw 'Advertising ID remains disabled' }",
  'disable-tailored-experiences': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop).TailoredExperiencesWithDiagnosticDataEnabled -eq 0) { throw 'Tailored experiences remain disabled' }",
  'disable-windows-feedback': "$feedbackPeriod = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction SilentlyContinue).NumberOfSIUFInPeriod; if ($feedbackPeriod -eq 0) { throw 'Windows feedback remains disabled' }; Write-Output 'Windows feedback prompts restored'",
  'disable-cloud-content': "$cloudContent = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction SilentlyContinue).DisableConsumerAccountStateContent; if ($cloudContent -eq 1) { throw 'Cloud consumer content remains disabled' }; Write-Output 'Cloud consumer content restored'",
  'disable-start-tracking': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop).Start_TrackProgs -eq 0) { throw 'Start tracking remains disabled' }",
  'disable-app-suggestions': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop).'SubscribedContent-338388Enabled' -eq 0) { throw 'App suggestions remain disabled' }",
  'disable-setting-sync': "$syncPolicy = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction SilentlyContinue).SyncPolicy; if ($syncPolicy -eq 5) { throw 'Settings sync remains disabled' }; Write-Output 'Settings sync restored'",
  'disable-handwriting-data': "$handwritingSharing = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction SilentlyContinue).PreventHandwritingDataSharing; if ($handwritingSharing -eq 1) { throw 'Handwriting data sharing remains disabled' }; Write-Output 'Handwriting data sharing restored'",
  'disable-speech-recognition': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop).HasAccepted -eq 0) { throw 'Online speech recognition remains disabled' }",
  'disable-find-my-device': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop).LocationSyncEnabled -eq 0) { throw 'Find My Device remains disabled' }",
  'disable-camera-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop).Value -eq 'Deny') { throw 'Camera access remains denied' }",
  'disable-microphone-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -ErrorAction Stop).Value -eq 'Deny') { throw 'Microphone access remains denied' }",
  'disable-network-throttling': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name NetworkThrottlingIndex -ErrorAction SilentlyContinue).NetworkThrottlingIndex -eq 4294967295) { throw 'Network throttling remains disabled' }",
  'optimize-network-power': "if (Get-NetAdapterPowerManagement -Name (Get-NetAdapter -Physical | Select-Object -First 1 -ExpandProperty Name) -ErrorAction Stop | Where-Object AllowComputerToTurnOffDevice -eq 'Disabled') { throw 'Network adapter power saving remains disabled' }",
  'disable-llmnr': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient' -Name EnableMulticast -ErrorAction SilentlyContinue).EnableMulticast -eq 0) { throw 'LLMNR remains disabled' }",
  'menu-delay': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name MenuShowDelay -ErrorAction Stop).MenuShowDelay -eq '0') { throw 'Menu delay remains reduced' }",
  'inactive-window-scroll': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name MouseWheelRouting -ErrorAction SilentlyContinue).MouseWheelRouting -eq 0) { throw 'Inactive window scrolling remains disabled' }",
  'disable-sticky-keys': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Accessibility\\StickyKeys' -Name Flags -ErrorAction Stop).Flags -eq '506') { throw 'Sticky Keys shortcut remains disabled' }",
  'disable-usb-suspend': "if ((powercfg /query SCHEME_CURRENT SUB_USB USBSELECTIVE | Out-String) -notmatch '0x00000001') { throw 'USB selective suspend remains disabled' }",
  'disable-hibernation': "$power = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -ErrorAction Stop; $hibernateEnabled = if ($power.PSObject.Properties.Name -contains 'HibernateEnabled') { $power.HibernateEnabled } else { 1 }; if ($hibernateEnabled -eq 0) { throw 'Hibernation remains disabled' }",
  'disable-fast-startup': "if ((Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled -ErrorAction Stop).HiberbootEnabled -eq 0) { throw 'Fast Startup remains disabled' }",
  'enable-hags': "if ((Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' -Name HwSchMode -ErrorAction Stop).HwSchMode -eq 2) { throw 'HAGS remains enabled' }",
  'keyboard-rate': "$keyboard = Get-ItemProperty 'HKCU:\\Control Panel\\Keyboard' -ErrorAction Stop; if ($keyboard.KeyboardDelay -eq '0' -and $keyboard.KeyboardSpeed -eq '31') { throw 'Keyboard settings remain optimized' }",
  'touchpad-latency': "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name AAPThreshold -ErrorAction SilentlyContinue).AAPThreshold -eq 0) { throw 'Touchpad latency remains reduced' }",
  'animations': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop\\WindowMetrics' -Name MinAnimate -ErrorAction SilentlyContinue).MinAnimate -eq '0') { throw 'Animations remain disabled' }",
  'shadows': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name VisualFXSetting -ErrorAction SilentlyContinue).VisualFXSetting -eq 2) { throw 'Visual effects remain optimized' }",
  'taskbar-icons': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarAl -ErrorAction SilentlyContinue).TaskbarAl -eq 0) { throw 'Taskbar remains left-aligned' }",
  'notifications': "if ((Get-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Name DisableNotificationCenter -ErrorAction SilentlyContinue).DisableNotificationCenter -eq 1) { throw 'Notifications remain disabled' }",
  'disable-error-reporting': "if ((Get-Service WerSvc -ErrorAction Stop).StartType -eq 'Disabled') { throw 'WER remains disabled' }",
  'disable-delivery-optimization': "if ((Get-Service DoSvc -ErrorAction Stop).StartType -eq 'Disabled') { throw 'Delivery Optimization remains disabled' }",
  'disable-windows-insider': "if (Get-Service wisvc -ErrorAction SilentlyContinue) { if ((Get-Service wisvc).StartType -eq 'Disabled') { throw 'Windows Insider service remains disabled' } }",
  'disable-retail-demo': "if (Get-Service RetailDemo -ErrorAction SilentlyContinue) { if ((Get-Service RetailDemo).StartType -eq 'Disabled') { throw 'Retail Demo service remains disabled' } }",
  'disable-power-throttling': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling' -Name PowerThrottlingOff -ErrorAction SilentlyContinue).PowerThrottlingOff -eq 1) { throw 'Power throttling remains disabled' }",
  'disable-cpu-idle': "if ((powercfg /query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN | Out-String) -match '0x00000064') { throw 'CPU idle remains disabled' }",
  'disable-core-parking': "if ((powercfg /query SCHEME_CURRENT SUB_PROCESSOR CPMINCORES | Out-String) -match '0x00000064') { throw 'Core parking remains disabled' }",
  'disable-memory-dumps': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\CrashControl' -Name CrashDumpEnabled -ErrorAction SilentlyContinue).CrashDumpEnabled -eq 0) { throw 'Memory dumps remain disabled' }",
  'enable-long-paths': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name LongPathsEnabled -ErrorAction SilentlyContinue).LongPathsEnabled -eq 1) { throw 'Long paths remain enabled' }",
  'disable-bits': "if ((Get-Service BITS -ErrorAction Stop).StartType -eq 'Disabled') { throw 'BITS remains disabled' }; if ((Get-Service BITS -ErrorAction Stop).Status -eq 'Stopped') { throw 'BITS remains stopped' }",
  'dns-optimization': "if (Get-NetAdapter -Physical -Status Up -ErrorAction Stop | Where-Object { $servers = @(Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction Stop).ServerAddresses; $servers -contains '1.1.1.1' -or $servers -contains '1.0.0.1' }) { throw 'Cloudflare DNS remains configured' }",
  'timer-resolution-0-5ms': "if (Test-Path (Join-Path $env:TEMP 'ca-o-timer-resolution.pid')) { throw 'Timer resolution helper remains active' }",
  'disable-activity-history': "$p=Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -ErrorAction SilentlyContinue; if ($p.PublishUserActivities -eq 0 -or $p.UploadUserActivities -eq 0) { throw 'Activity history remains disabled' }",
  'disable-location-tracking': "if ((Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Location tracking remains denied' }",
  'disable-input-personalization': "$p=Get-ItemProperty 'HKCU:\\Software\\Microsoft\\InputPersonalization' -ErrorAction SilentlyContinue; if ($p.RestrictImplicitInkCollection -eq 1 -or $p.RestrictImplicitTextCollection -eq 1) { throw 'Input personalization remains restricted' }",
  'disable-contacts-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\contacts' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Contacts access remains denied' }",
  'disable-calendar-access': "if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appointments' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Calendar access remains denied' }",

  // ─── NEW BATCH (2026-08) ──────────────────────────────────────────────
  'disable-widgets': "$allowNews = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Dsh' -Name AllowNewsAndInterests -ErrorAction SilentlyContinue).AllowNewsAndInterests; if ($allowNews -eq 0) { throw 'Widgets policy remains applied' }; Write-Output 'Widgets policy restored'",
  'disable-recall': "$recallValue = (Get-ItemProperty 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableAIDataAnalysis -ErrorAction SilentlyContinue).DisableAIDataAnalysis; if ($recallValue -eq 1) { throw 'Recall remains disabled' }; Write-Output 'Recall policy restored'",
  'disable-netbios': "$netbiosLeftover = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NetBT\\Parameters\\Interfaces' | Where-Object { (Get-ItemProperty $_.PSPath -Name NetbiosOptions -ErrorAction SilentlyContinue).NetbiosOptions -ne $null }; if ($netbiosLeftover) { throw 'NetBIOS overrides remain configured' }; Write-Output 'NetBIOS settings restored'",
  'disable-smb1': "if (-not (Get-SmbServerConfiguration -ErrorAction Stop).EnableSMB1Protocol) { throw 'SMBv1 remains disabled' }",
  'show-seconds-clock': "$secondsClock = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowSecondsInSystemClock -ErrorAction SilentlyContinue).ShowSecondsInSystemClock; if ($secondsClock -eq 1) { throw 'Seconds clock remains enabled' }; Write-Output 'Clock restored to default'",
  'hide-meet-now': "$meetNow = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowMeetNow -ErrorAction SilentlyContinue).ShowMeetNow; if ($meetNow -eq 0) { throw 'Meet Now icon remains hidden' }; Write-Output 'Meet Now icon restored'",
  'disable-fullscreen-optimizations': "$fsoBehavior = (Get-ItemProperty 'HKCU:\\System\\GameConfigStore' -Name GameDVR_FSOBehavior -ErrorAction SilentlyContinue).GameDVR_FSOBehavior; if ($fsoBehavior -eq 1) { throw 'Fullscreen optimizations remain disabled' }; Write-Output 'Fullscreen optimizations restored'",
  'disable-welcome-experience': "$welcomeContent = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -ErrorAction SilentlyContinue).'SubscribedContent-310093Enabled'; if ($welcomeContent -eq 0) { throw 'Welcome experience remains disabled' }; Write-Output 'Welcome experience restored'",

  // ─── CATEGORY BATCH x10 EACH (2026-08) ────────────────────────────────
  'disable-ceip-tasks': "$ceipTasks = @(@('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','Consolidator'), @('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','UsbCeip'), @('\\Microsoft\\Windows\\Application Experience\\','Microsoft Compatibility Appraiser'), @('\\Microsoft\\Windows\\Application Experience\\','ProgramDataUpdater')); foreach ($entry in $ceipTasks) { $task = Get-ScheduledTask -TaskPath $entry[0] -TaskName $entry[1] -ErrorAction SilentlyContinue; if ($task -and $task.State -eq 'Disabled') { throw \"Scheduled task $($entry[1]) remains disabled\" } }; Write-Output 'CEIP scheduled tasks restored'",
  'disable-last-access-time': "if (-not ((fsutil behavior query disablelastaccess | Out-String) -match '(?i)DisableLastAccess\\s*=\\s*1')) { Write-Output 'Last-access updates restored' } else { throw 'Last-access updates remain disabled' }",
  'disable-8dot3-names': "if (-not ((fsutil behavior query disable8dot3 | Out-String) -match '(?i)Disable8dot3\\s*=\\s*1')) { Write-Output 'Short name creation restored' } else { throw '8.3 name creation remains disabled' }",
  'disable-admin-shares': "$shares = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters' -ErrorAction SilentlyContinue; if ($shares.AutoShareServer -eq 0 -or $shares.AutoShareWks -eq 0) { throw 'Administrative shares remain blocked' }; Write-Output 'Administrative shares restored'",
  'disable-remote-assistance': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance' -Name fAllowToGetHelp -ErrorAction SilentlyContinue).fAllowToGetHelp -eq 0) { throw 'Remote Assistance remains disabled' }; Write-Output 'Remote Assistance restored'",
  'disable-remote-desktop': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server' -Name fDenyTSConnections -ErrorAction SilentlyContinue).fDenyTSConnections -eq 1) { throw 'Remote Desktop remains blocked' }; Write-Output 'Remote Desktop restored'",
  'speedup-shutdown': "$ctrlCheck = Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name WaitToKillServiceTimeout -ErrorAction SilentlyContinue; if ($ctrlCheck.WaitToKillServiceTimeout -eq '2000') { throw 'Shutdown speedup remains applied' }; Write-Output 'Shutdown timeouts restored'",
  'no-auto-reboot-active': "$noRebootCheck = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU' -Name NoAutoRebootWithLoggedOnUsers -ErrorAction SilentlyContinue).NoAutoRebootWithLoggedOnUsers; if ($noRebootCheck -eq 1) { throw 'No-reboot policy remains applied' }; Write-Output 'Automatic reboot policy restored'",
  'disable-driver-search': "$driverSearchCheck = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching' -Name SearchOrderConfig -ErrorAction SilentlyContinue).SearchOrderConfig; if ($driverSearchCheck -eq 0) { throw 'Driver search remains disabled' }; Write-Output 'Driver search restored'",

  'disable-hotspot-service': "if (Get-Service -Name icssvc -ErrorAction SilentlyContinue) { if ((Get-Service -Name icssvc).StartType -eq 'Disabled') { throw 'Mobile Hotspot service remains disabled' } }; Write-Output 'Hotspot service restored'",
  'require-network-level-auth': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp' -Name UserAuthentication -ErrorAction SilentlyContinue).UserAuthentication -eq 1) { throw 'NLA requirement remains enforced' }; Write-Output 'NLA requirement relaxed'",
  'disable-wpad': "if (Get-Service -Name WinHttpAutoProxySvc -ErrorAction SilentlyContinue) { if ((Get-Service -Name WinHttpAutoProxySvc).StartType -eq 'Disabled') { throw 'WPAD service remains disabled' } }; Write-Output 'WPAD service restored'",
  'disable-active-probing': "if ((Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NlaSvc\\Parameters\\Internet' -Name EnableActiveProbing -ErrorAction SilentlyContinue).EnableActiveProbing -eq 0) { throw 'Active probing remains disabled' }; Write-Output 'Connectivity probes restored'",
  'disable-peer-name-resolution': "if (Get-Service -Name PNRPsvc -ErrorAction SilentlyContinue) { if ((Get-Service -Name PNRPsvc).StartType -eq 'Disabled') { throw 'PNRP service remains disabled' } }; Write-Output 'PNRP service restored'",
  'restrict-point-and-print': "$papCheck = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\Printers\\PointAndPrint' -Name RestrictDriverInstallationToAdministrators -ErrorAction SilentlyContinue).RestrictDriverInstallationToAdministrators; if ($papCheck -eq 1) { throw 'Point and Print restriction remains applied' }; Write-Output 'Printer driver installs unrestricted'",
  'disable-ssdp-discovery': "if (Get-Service -Name SSDPSRV -ErrorAction SilentlyContinue) { if ((Get-Service -Name SSDPSRV).StartType -eq 'Disabled') { throw 'SSDP discovery remains disabled' } }; Write-Output 'SSDP discovery restored'",
  'disable-upnp-device-host': "if (Get-Service -Name upnphost -ErrorAction SilentlyContinue) { if ((Get-Service -Name upnphost).StartType -eq 'Disabled') { throw 'UPnP Device Host remains disabled' } }; Write-Output 'UPnP host restored'",
  'disable-snmp-trap': "if (Get-Service -Name SNMPTRAP -ErrorAction SilentlyContinue) { if ((Get-Service -Name SNMPTRAP).StartType -eq 'Disabled') { throw 'SNMP Trap remains disabled' } }; Write-Output 'SNMP Trap restored'",

  'disable-filter-keys': "$fk = (Get-ItemProperty 'HKCU:\\Control Panel\\Accessibility\\FilterKeys' -Name Flags -ErrorAction SilentlyContinue).Flags; if ($fk -eq '122') { throw 'Filter Keys shortcut remains disabled' }; Write-Output 'Filter Keys shortcut restored'",
  'disable-toggle-keys': "$tk = (Get-ItemProperty 'HKCU:\\Control Panel\\Accessibility\\ToggleKeys' -Name Flags -ErrorAction SilentlyContinue).Flags; if ($tk -eq '58') { throw 'Toggle Keys shortcut remains disabled' }; Write-Output 'Toggle Keys shortcut restored'",
  'disable-touch-keyboard-autoinvoke': "$tt = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\TabletTip\\1.7' -Name EnableDesktopModeAutoInvoke -ErrorAction SilentlyContinue).EnableDesktopModeAutoInvoke; if ($tt -eq 0) { throw 'Touch keyboard auto-invoke remains off' }; Write-Output 'Touch keyboard auto-invoke restored'",
  'disable-controller-gamebar-chord': "$nexus = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\GameBar' -Name UseNexusForGameBarEnabled -ErrorAction SilentlyContinue).UseNexusForGameBarEnabled; if ($nexus -eq 0) { throw 'Controller Game Bar chord remains off' }; Write-Output 'Controller chord restored'",
  'disable-touchpad-edge-swipes': "$edgeSwipe = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name EdgeSwipeEnabled -ErrorAction SilentlyContinue).EdgeSwipeEnabled; if ($edgeSwipe -eq 0) { throw 'Edge swipes remain disabled' }; Write-Output 'Edge swipes restored'",
  'disable-touchpad-threefinger-slide': "$threeSlide = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name ThreeFingerSlideEnabled -ErrorAction SilentlyContinue).ThreeFingerSlideEnabled; if ($threeSlide -eq 0) { throw 'Three-finger slide remains disabled' }; Write-Output 'Three-finger slide restored'",
  'disable-windows-ink': "$inkCheck = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsInkWorkspace' -Name AllowWindowsInkWorkspace -ErrorAction SilentlyContinue).AllowWindowsInkWorkspace; if ($inkCheck -eq 0) { throw 'Windows Ink remains disabled' }; Write-Output 'Windows Ink restored'",
  'numlock-on-boot': "$numUserCheck = (Get-ItemProperty 'HKCU:\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -ErrorAction SilentlyContinue).InitialKeyboardIndicators; $numDefaultCheck = (Get-ItemProperty 'Registry::HKEY_USERS\\.DEFAULT\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -ErrorAction SilentlyContinue).InitialKeyboardIndicators; if ($numUserCheck -eq '2' -or $numDefaultCheck -eq '2') { throw 'NumLock-on-boot preference remains forced' }; Write-Output 'NumLock boot behavior restored'",
  'disable-hover-checkboxes': "$autoCheck = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name AutoCheckSelect -ErrorAction SilentlyContinue).AutoCheckSelect; if ($autoCheck -eq 0) { throw 'Hover checkboxes remain disabled' }; Write-Output 'Hover checkboxes restored'",
  'disable-tablet-input-service': "if (Get-Service -Name TabletInputService -ErrorAction SilentlyContinue) { if ((Get-Service -Name TabletInputService).StartType -eq 'Disabled') { throw 'Tablet input service remains disabled' } }; Write-Output 'Tablet input service restored'",

  'delay-taskbar-thumbnails': "$hoverTime = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ExtendedUIHoverTime -ErrorAction SilentlyContinue).ExtendedUIHoverTime; if ($hoverTime -eq 30000) { throw 'Thumbnail delay remains applied' }; Write-Output 'Thumbnail previews restored'",
  'hide-start-recommended': "$recCheck = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer' -Name HideRecommendedSection -ErrorAction SilentlyContinue).HideRecommendedSection; if ($recCheck -eq 1) { throw 'Start recommendations remain hidden' }; Write-Output 'Start recommendations shown again'",
  'disable-drag-full-window': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name DragFullWindows -ErrorAction SilentlyContinue).DragFullWindows -eq '0') { throw 'Drag full window remains off' }; Write-Output 'Drag full window restored'",
  'never-combine-taskbar-icons': "$glomLevel = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarGlomLevel -ErrorAction SilentlyContinue).TaskbarGlomLevel; if ($glomLevel -eq 2) { throw 'Taskbar buttons remain uncombined' }; Write-Output 'Taskbar grouping restored'",
  'disable-window-shake': "$shakeCheck = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisableViewShake -ErrorAction SilentlyContinue).DisableViewShake; if ($shakeCheck -eq 1) { throw 'Window shake remains disabled' }; Write-Output 'Window shake restored'",
  'disable-snap-layouts-flyout': "$flyoutCheck = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name EnableSnapAssistFlyout -ErrorAction SilentlyContinue).EnableSnapAssistFlyout; if ($flyoutCheck -eq 0) { throw 'Snap layouts flyout remains disabled' }; Write-Output 'Snap layouts flyout restored'",
  'disable-window-arrange-drag': "if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name WindowArrangementActive -ErrorAction SilentlyContinue).WindowArrangementActive -eq '0') { throw 'Drag arrangement remains off' }; Write-Output 'Drag arrangement restored'",
  'disable-spotlight-wallpapers': "$spotlightCheck = Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -ErrorAction SilentlyContinue; if ($spotlightCheck.RotatingLockScreenEnabled -eq 0 -or $spotlightCheck.RotatingLockScreenOverlayEnabled -eq 0) { throw 'Spotlight lock screen remains off' }; Write-Output 'Spotlight wallpapers restored'",
  'hide-copilot-button': "$copilotCheck = (Get-ItemProperty 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot' -Name TurnOffWindowsCopilot -ErrorAction SilentlyContinue).TurnOffWindowsCopilot; if ($copilotCheck -eq 1) { throw 'Copilot button remains hidden' }; Write-Output 'Copilot button restored'",

  'optimize-thread-scheduling': "$threadSep = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl' -Name Win32PrioritySeparation -ErrorAction SilentlyContinue).Win32PrioritySeparation; if ($threadSep -eq 38) { throw 'Thread scheduling remains optimized' }; Write-Output 'Default thread scheduling restored'",
  'disable-svchost-split-threshold': "$splitThreshold = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name SvcHostSplitThresholdInKB -ErrorAction SilentlyContinue).SvcHostSplitThresholdInKB; if ($splitThreshold -eq 4294967295) { throw 'svchost split threshold remains overridden' }; Write-Output 'svchost splitting restored'",
  'optimize-ntfs-memory-usage': "if (-not ((fsutil behavior query memoryusage | Out-String) -match '(?i)MemoryUsage\\s*=\\s*2')) { Write-Output 'NTFS memory usage restored' } else { throw 'NTFS memory usage remains raised' }",
  'disable-modern-standby': "$standbyOverride = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name PlatformAoAcOverride -ErrorAction SilentlyContinue).PlatformAoAcOverride; if ($standbyOverride -eq 0) { throw 'Modern Standby remains overridden' }; Write-Output 'Modern Standby restored'",
  'disable-edge-startup-boost': "$edgeBoost = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge' -Name StartupBoostEnabled -ErrorAction SilentlyContinue).StartupBoostEnabled; if ($edgeBoost -eq 0) { throw 'Edge startup boost remains disabled' }; Write-Output 'Edge startup boost restored'",
  'disable-automatic-maintenance': "$maintenanceDisabled = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Schedule\\Maintenance' -Name MaintenanceDisabled -ErrorAction SilentlyContinue).MaintenanceDisabled; if ($maintenanceDisabled -eq 1) { throw 'Automatic maintenance remains disabled' }; Write-Output 'Automatic maintenance restored'",
  'disable-app-readiness': "if (Get-Service -Name AppReadiness -ErrorAction SilentlyContinue) { if ((Get-Service -Name AppReadiness).StartType -eq 'Disabled') { throw 'App Readiness remains disabled' } }; Write-Output 'App Readiness restored'",
  'disable-ssl-time-seeding': "$sslSeedCheck = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\W32Time\\Config' -Name UtilizeSslTimeData -ErrorAction SilentlyContinue).UtilizeSslTimeData; if ($sslSeedCheck -eq 0) { throw 'SSL time seeding remains disabled' }; Write-Output 'SSL time seeding restored'",
  'max-system-responsiveness': "$sysResp = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name SystemResponsiveness -ErrorAction SilentlyContinue).SystemResponsiveness; if ($sysResp -eq 0) { throw 'System responsiveness remains maximized' }; Write-Output 'Default system responsiveness restored'",
  'disable-memory-integrity': "$hvciCheck = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity' -Name Enabled -ErrorAction SilentlyContinue).Enabled; if ($hvciCheck -eq 0) { throw 'Memory Integrity remains disabled' }; Write-Output 'Memory Integrity re-enabled'",
  'disable-click-to-do': "$ctdCheck = (Get-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableClickToDo -ErrorAction SilentlyContinue).DisableClickToDo; if ($ctdCheck -eq 1) { throw 'Click to Do remains blocked' }; Write-Output 'Click to Do restored'",
  'disable-paint-ai': "$paintAi = Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Paint' -ErrorAction SilentlyContinue; if ($paintAi.DisableCocreator -eq 1 -or $paintAi.DisableImageGenerator -eq 1) { throw 'Paint AI features remain disabled' }; Write-Output 'Paint AI features restored'",
  'static-pagefile': "$pf = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management' -Name PagingFiles -ErrorAction SilentlyContinue).PagingFiles[0]; if ($pf -match 'pagefile\\.sys \\d+ \\d+$') { throw 'Static pagefile remains configured' }; Write-Output 'Automatic pagefile restored'",
  'windowed-games-optimization': "$wgo = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences' -Name DirectXUserGlobalSettings -ErrorAction SilentlyContinue).DirectXUserGlobalSettings; if ($wgo -match 'SwapEffectUpgradeEnable=1') { throw 'Windowed games optimization remains enabled' }; Write-Output 'Default window swap restored'",
  'disable-multiplane-overlay': "$mpo = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm' -Name OverlayTestMode -ErrorAction SilentlyContinue).OverlayTestMode; if ($mpo -eq 5) { throw 'Multi-Plane Overlay remains disabled' }; Write-Output 'Multi-Plane Overlay restored'",

  'disable-clipboard-history': "$clipHistory = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Clipboard' -Name EnableClipboardHistory -ErrorAction SilentlyContinue).EnableClipboardHistory; if ($clipHistory -eq 0) { throw 'Clipboard history remains disabled' }; Write-Output 'Clipboard history restored'",
  'disable-clipboard-cloud-sync': "$cloudClip = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Clipboard' -Name CloudClipboardAutomaticUpload -ErrorAction SilentlyContinue).CloudClipboardAutomaticUpload; if ($cloudClip -eq 0) { throw 'Cloud clipboard upload remains blocked' }; Write-Output 'Cloud clipboard upload restored'",
  'deny-user-account-information': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\userAccountInformation' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Account information access remains denied' }; Write-Output 'Account information access allowed'",
  'deny-documents-library': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\documentsLibrary' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Documents access remains denied' }; Write-Output 'Documents access allowed'",
  'deny-pictures-library': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\picturesLibrary' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Pictures access remains denied' }; Write-Output 'Pictures access allowed'",
  'deny-videos-library': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\videosLibrary' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Videos access remains denied' }; Write-Output 'Videos access allowed'",
  'deny-email-access': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\email' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Email access remains denied' }; Write-Output 'Email access allowed'",
  'deny-radios-access': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\radios' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Radios access remains denied' }; Write-Output 'Radios access allowed'",
  'deny-human-presence': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\humanPresence' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Presence sensor access remains denied' }; Write-Output 'Presence sensor access allowed'",
  'deny-broad-filesystem': "if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\broadFileSystemAccess' -Name Value -ErrorAction SilentlyContinue).Value -eq 'Deny') { throw 'Broad filesystem access remains denied' }; Write-Output 'Broad filesystem access allowed'",
};

export const originalStateCommands: Record<string, string> = {
  'disable-startup-sound': "try { $value = (Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop).'(Default)'; [pscustomobject]@{ exists = $true; value = [string]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-cortana': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop).AllowCortana; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-screen-saver': "try { $value = (Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name ScreenSaveActive -ErrorAction Stop).ScreenSaveActive; [pscustomobject]@{ exists = $true; value = [string]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-mouse-trails': "try { $value = (Get-ItemProperty 'HKCU:\\Control Panel\\Mouse' -Name MouseTrails -ErrorAction Stop).MouseTrails; [pscustomobject]@{ exists = $true; value = [string]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'hide-task-view': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop).ShowTaskViewButton; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-aero-peek': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop).DisablePreviewDesktop; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-tooltips': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop).ShowInfoTip; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-wallpaper-slideshow': "try { $value = (Get-ItemProperty 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop).Interval; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-system-sounds': "try { $value = (Get-ItemProperty 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -ErrorAction Stop).'(Default)'; [pscustomobject]@{ exists = $true; value = [string]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'show-hidden-files': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop).Hidden; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-start-menu-suggestions': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop).SystemPaneSuggestionsEnabled; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-taskbar-search': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop).SearchboxTaskbarMode; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'show-file-extensions': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop).HideFileExt; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-background-apps': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction Stop).GlobalUserDisabled; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-cast-notifications': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction Stop).AllowWhileLocked; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-thumbnails': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop).IconsOnly; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-lock-screen': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction Stop).NoLockScreen; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-advertising-id': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop).Enabled; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-tailored-experiences': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop).TailoredExperiencesWithDiagnosticDataEnabled; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-windows-feedback': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction Stop).NumberOfSIUFInPeriod; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-cloud-content': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction Stop).DisableConsumerAccountStateContent; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-start-tracking': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop).Start_TrackProgs; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-app-suggestions': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop).'SubscribedContent-338388Enabled'; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-setting-sync': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction Stop).SyncPolicy; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-handwriting-data': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction Stop).PreventHandwritingDataSharing; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-speech-recognition': "try { $value = (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop).HasAccepted; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-find-my-device': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop).LocationSyncEnabled; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-diagnostic-data': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction Stop).AllowTelemetry; [pscustomobject]@{ exists = $true; value = [int]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-camera-access': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop).Value; [pscustomobject]@{ exists = $true; value = [string]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'disable-microphone-access': "try { $value = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -ErrorAction Stop).Value; [pscustomobject]@{ exists = $true; value = [string]$value } | ConvertTo-Json -Compress } catch { [pscustomobject]@{ exists = $false } | ConvertTo-Json -Compress }",
  'dns-optimization': "$adapters = @(Get-NetAdapter -Physical -Status Up -ErrorAction Stop | ForEach-Object { $dns = @(Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses; [pscustomobject]@{ interfaceIndex = [int]$_.ifIndex; servers = @($dns) } }); [pscustomobject]@{ exists = $true; value = ($adapters | ConvertTo-Json -Compress) } | ConvertTo-Json -Compress",
};

export const antiCheatWarnings: Record<string, { es: string; en: string }> = {
  'timer-resolution-0-5ms': {
    es: 'VEREDICTO ANTI-CHEAT: no es trampa: usa NtSetTimerResolution, una API pública que también usan navegadores y Spotify; ningún anti-cheat banea por ella. Precaución práctica: deja un proceso powershell.exe oculto sosteniendo los 0,5 ms, y heurísticas de monitoreo pueden verlo como proceso raro. Revierte antes de jugar ranked si quieres cero procesos extra.',
    en: 'ANTI-CHEAT VERDICT: not cheating: it uses NtSetTimerResolution, a public API also used by browsers and Spotify; no anti-cheat bans for it. Practical caution: it leaves a hidden powershell.exe holding the 0.5 ms request, which monitoring heuristics may flag as an odd process. Revert before ranked play if you want zero extra processes.',
  },
  'clear-temp-files': {
    es: 'VEREDICTO ANTI-CHEAT: no es trampa ni se detecta. Riesgo real aparte: vaciar %TEMP% puede eliminar componentes ya instalados de EasyAntiCheat o BattlEye y el juego dejará de abrir hasta repararlos (Verificar archivos / EasyAntiCheat_setup). Vanguard no se ve afectado (no vive en TEMP).',
    en: 'ANTI-CHEAT VERDICT: not cheating and not detected. Separate real risk: emptying %TEMP% can delete installed EasyAntiCheat or BattlEye components so the game stops launching until repaired (Verify files / EasyAntiCheat_setup). Vanguard is unaffected (it does not live in TEMP).',
  },
  'disable-xbox-gamebar': {
    es: 'VEREDICTO ANTI-CHEAT: no es trampa: son valores oficiales de Windows expuestos en su propia Configuración; ningún anti-cheat los detecta como manipulación. Compatibilidad: algunos juegos/overlays dependen de Game Bar para captura o chat.',
    en: 'ANTI-CHEAT VERDICT: not cheating: these are official Windows values exposed in its own Settings; no anti-cheat detects them as tampering. Compatibility: some games/overlays rely on Game Bar for capture or chat.',
  },
  'disable-game-dvr': {
    es: 'VEREDICTO ANTI-CHEAT: no es trampa: equivalente a apagar "Grabación de fondo" en Configuración; sin detección ni baneos conocidos. Puede afectar capturas/overlay de juegos que dependan de Game DVR.',
    en: 'ANTI-CHEAT VERDICT: not cheating: equivalent to turning off "Background recording" in Settings; no known detections or bans. May affect capture/overlay in games relying on Game DVR.',
  },
  'disable-services': {
    es: 'VEREDICTO ANTI-CHEAT: no es trampa en sí, pero deshabilitar servicios puede romper EasyAntiCheat, BattlEye o Xbox Services si tocas uno que necesiten. Esta opción solo apaga MapsBroker y Fax (riesgo bajo); si un juego no abre, revierte.',
    en: 'ANTI-CHEAT VERDICT: not cheating by itself, but disabling services can break EasyAntiCheat, BattlEye, or Xbox Services if you hit one they need. This option only stops MapsBroker and Fax (low risk); revert if a game fails to launch.',
  },
  'disable-fullscreen-optimizations': {
    es: 'VEREDICTO ANTI-CHEAT: no es trampa: equivale exactamente al casilla por-juego "Deshabilitar optimizaciones de pantalla completa" de Windows; sin detecciones ni baneos conocidos. Algunos títulos antiguos pueden comportarse distinto en exclusiva.',
    en: 'ANTI-CHEAT VERDICT: not cheating: identical to the per-game "Disable fullscreen optimizations" checkbox in Windows; no known detections or bans. Some older titles may behave differently in true exclusive mode.',
  },
};

const baseRevertCommands: Record<string, {
  commands: { description: string; script: string }[];
  rebootRequired: boolean;
}> = {
  // SYSTEM
  'disable-telemetry': {
    commands: [{ description: "Re-enabling Telemetry", script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name 'AllowTelemetry' -Value 3 -Type DWord -Force -ErrorAction Stop; Set-Service -Name 'DiagTrack' -StartupType Automatic -ErrorAction Stop; Start-Service -Name 'DiagTrack' -ErrorAction Stop;" }],
    rebootRequired: true
  },
  'disable-cortana': {
    commands: [{ description: "Re-enabling Cortana", script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name 'AllowCortana' -Value 1 -Type DWord -Force -ErrorAction Stop;" }],
    rebootRequired: true
  },
  'disable-search-indexing': {
    commands: [{ description: "Re-enabling Search Indexing", script: "Set-Service -Name 'WSearch' -StartupType Automatic -ErrorAction Stop; Start-Service -Name 'WSearch' -ErrorAction Stop;" }],
    rebootRequired: false
  },
  'disable-superfetch': {
    commands: [{ description: "Re-enabling Superfetch", script: "Set-Service -Name 'SysMain' -StartupType Automatic -ErrorAction Stop; Start-Service -Name 'SysMain' -ErrorAction Stop;" }],
    rebootRequired: true
  },
  'disable-print-spooler': {
    commands: [{ description: "Re-enabling Print Spooler", script: "Set-Service -Name 'Spooler' -StartupType Automatic -ErrorAction Stop; Start-Service -Name 'Spooler' -ErrorAction Stop;" }],
    rebootRequired: false
  },
  'disable-xbox-gamebar': {
    commands: [{ description: "Re-enabling Xbox Game Bar", script: "Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR' -Name 'AppCaptureEnabled' -Value 1 -Type DWord -Force -ErrorAction Stop; Set-ItemProperty -Path 'HKCU:\\System\\GameConfigStore' -Name 'GameDVR_Enabled' -Value 1 -Type DWord -Force -ErrorAction Stop;" }],
    rebootRequired: true
  },
  'optimize-startup': {
    commands: [{ description: "Startup changes require manual Windows configuration", script: "throw 'Startup optimization has no automatic revert implementation'" }],
    rebootRequired: false
  },
  'clear-temp-files': {
    commands: [{ description: "Nothing to revert", script: "Write-Output 'Files deleted permanently'" }],
    rebootRequired: false
  },

  // NETWORK
  'dns-optimization': {
    commands: [{ description: "DNS settings must be restored from the captured adapter baseline", script: "throw 'DNS optimization has no automatic revert implementation'" }],
    rebootRequired: false
  },
  'winsock-reset': {
    commands: [{ description: "Winsock reset requires a Windows network reset", script: "throw 'Winsock reset has no automatic revert implementation'" }],
    rebootRequired: false
  },
  'flush-dns': {
    commands: [{ description: "DNS cache flush has no persistent setting to restore", script: "throw 'DNS cache flush has no automatic revert implementation'" }],
    rebootRequired: false
  },
  'reset-network': {
    commands: [{ description: "Network stack reset requires a Windows network reset", script: "throw 'Network stack reset has no automatic revert implementation'" }],
    rebootRequired: false
  },

  // INPUT
  'mouse-acceleration': {
    commands: [{ description: "Re-enabling Mouse Acceleration", script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name 'MouseSpeed' -Value 1 -Force -ErrorAction Stop;" }],
    rebootRequired: true
  },
  'keyboard-rate': {
    commands: [{ description: "Reverting Keyboard Rate", script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Keyboard' -Name 'KeyboardDelay' -Value 1 -Force; Set-ItemProperty -Path 'HKCU:\\Control Panel\\Keyboard' -Name 'KeyboardSpeed' -Value 31 -Force;" }],
    rebootRequired: true
  },
  'touchpad-latency': {
    commands: [{ description: "Reverting Touchpad Latency", script: "Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name 'AAPThreshold' -Value 2 -Force -ErrorAction SilentlyContinue;" }],
    rebootRequired: false
  },
  'mouse-polling': {
    commands: [{ description: "Mouse polling is controlled by vendor software", script: "throw 'Mouse polling has no automatic revert implementation'" }],
    rebootRequired: false
  },

  // TWEAKS
  'animations': {
    commands: [{ description: "Re-enabling UI Animations", script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop\\WindowMetrics' -Name 'MinAnimate' -Value '1' -Force -ErrorAction SilentlyContinue;" }],
    rebootRequired: false
  },
  'transparency': {
    commands: [{ description: "Re-enabling Transparency", script: "Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize' -Name 'EnableTransparency' -Value 1 -Type DWord -Force -ErrorAction Stop;" }],
    rebootRequired: false
  },
  'shadows': {
    commands: [{ description: "Re-enabling Window Shadows", script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name 'VisualFXSetting' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue;" }],
    rebootRequired: false
  },
  'taskbar-icons': {
    commands: [{ description: "Centering Taskbar (Win11)", script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarAl' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue;" }],
    rebootRequired: false
  },
  'notifications': {
    commands: [{ description: "Re-enabling Notifications", script: "Remove-ItemProperty -Path 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer' -Name 'DisableNotificationCenter' -Force -ErrorAction SilentlyContinue;" }],
    rebootRequired: true
  },

  // POWERFUL
  'power-plan': {
    commands: [{ description: "Reverting to Balanced Power Plan", script: "powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'Could not restore the Balanced power plan' }; Remove-Item (Join-Path $env:TEMP 'ca-o-powerplan.guid') -Force -ErrorAction SilentlyContinue" }],
    rebootRequired: false
  },
  'gaming-mode': {
    commands: [{ description: "Disabling Game Mode", script: "Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\GameBar' -Name 'AllowAutoGameMode' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\GameBar' -Name 'AutoGameModeEnabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue;" }],
    rebootRequired: false
  },
  'disable-services': {
    commands: [{ description: "Re-enabling Services", script: "Set-Service -Name 'MapsBroker' -StartupType Automatic -ErrorAction SilentlyContinue; Set-Service -Name 'Fax' -StartupType Manual -ErrorAction SilentlyContinue;" }],
    rebootRequired: true
  },
  'registry-cleanup': {
    commands: [{ description: "Registry cleanup requires restoring the captured MRU baseline", script: "throw 'Registry cleanup has no automatic revert implementation'" }],
    rebootRequired: false
  },
  'memory-compression': {
    commands: [{ description: "Re-enabling Memory Compression", script: "Enable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue;" }],
    rebootRequired: true
  }
};

export const revertCommands = {
  ...baseRevertCommands,
  'disable-cloud-content': { commands: [{ description: 'Restoring cloud consumer content', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-app-suggestions': { commands: [{ description: 'Restoring app suggestions', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-start-tracking': { commands: [{ description: 'Restoring Start app tracking', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-setting-sync': { commands: [{ description: 'Restoring settings synchronization', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-input-personalization': { commands: [{ description: 'Restoring input personalization', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\InputPersonalization' -Name RestrictImplicitInkCollection -ErrorAction SilentlyContinue; Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\InputPersonalization' -Name RestrictImplicitTextCollection -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-handwriting-data': { commands: [{ description: 'Restoring handwriting data sharing', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-speech-recognition': { commands: [{ description: 'Restoring online speech recognition', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-find-my-device': { commands: [{ description: 'Restoring Find My Device', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-contacts-access': { commands: [{ description: 'Restoring contacts access', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\contacts' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'disable-calendar-access': { commands: [{ description: 'Restoring calendar access', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appointments' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'disable-camera-access': { commands: [{ description: 'Restoring camera access', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'disable-microphone-access': { commands: [{ description: 'Restoring microphone access', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'disable-error-reporting': { commands: [{ description: 'Re-enabling Windows Error Reporting', script: "Set-Service -Name WerSvc -StartupType Manual -ErrorAction Stop; Start-Service -Name WerSvc -ErrorAction Stop" }], rebootRequired: false },
  'disable-delivery-optimization': { commands: [{ description: 'Re-enabling Delivery Optimization', script: "Set-Service -Name DoSvc -StartupType Automatic -ErrorAction Stop; Start-Service -Name DoSvc -ErrorAction Stop" }], rebootRequired: false },
  'disable-windows-insider': { commands: [{ description: 'Restoring Windows Insider service', script: "if (Get-Service -Name wisvc -ErrorAction SilentlyContinue) { Set-Service -Name wisvc -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'disable-retail-demo': { commands: [{ description: 'Restoring Retail Demo service', script: "if (Get-Service -Name RetailDemo -ErrorAction SilentlyContinue) { Set-Service -Name RetailDemo -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'disable-network-throttling': { commands: [{ description: 'Restoring network throttling', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name NetworkThrottlingIndex -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'optimize-network-power': { commands: [{ description: 'Restoring network adapter power management', script: "Get-NetAdapter -Physical | ForEach-Object { Set-NetAdapterPowerManagement -Name $_.Name -AllowComputerToTurnOffDevice Enabled -ErrorAction Stop }" }], rebootRequired: false },
  'enable-mouse-raw-input': { commands: [{ description: 'Restoring mouse trails setting', script: "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name MouseTrails -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-lock-screen': { commands: [{ description: 'Re-enabling lock screen', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-aero-peek': { commands: [{ description: 'Re-enabling Aero Peek preview', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-startup-sound': { commands: [{ description: 'Restoring startup sound', script: "Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -Value '.Current' -Force" }], rebootRequired: false },
  'disable-cast-notifications': { commands: [{ description: 'Restoring cast notifications', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-background-apps': { commands: [{ description: 'Re-enabling background apps', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-start-menu-suggestions': { commands: [{ description: 'Re-enabling Start menu suggestions', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-game-dvr': { commands: [{ description: 'Re-enabling Game DVR', script: "Set-ItemProperty -Path 'HKCU:\\System\\GameConfigStore' -Name GameDVR_Enabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-power-throttling': { commands: [{ description: 'Restoring power throttling', script: "Remove-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling' -Name PowerThrottlingOff -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'enable-msi-gpu': { commands: [{ description: 'Restoring GPU interrupt policy', script: 'Write-Output "GPU MSI policy requires the vendor default"' }], rebootRequired: true },
  'disable-cpu-idle': { commands: [{ description: 'Restoring processor minimum state', script: 'powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5; powercfg /setactive SCHEME_CURRENT' }], rebootRequired: false },
  'disable-core-parking': { commands: [{ description: 'Restoring default core parking behavior (parking allowed)', script: 'powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 0; powercfg /setactive SCHEME_CURRENT' }], rebootRequired: false },
  'disable-memory-dumps': { commands: [{ description: 'Re-enabling automatic memory dumps', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\CrashControl' -Name CrashDumpEnabled -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'enable-long-paths': { commands: [{ description: 'Restoring Win32 path policy', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' -Name LongPathsEnabled -Value 0 -Type DWord -Force" }], rebootRequired: true },
  'timer-resolution-0-5ms': { commands: [{ description: 'Stopping the temporary timer-resolution request', script: "$pidPath=Join-Path $env:TEMP 'ca-o-timer-resolution.pid'; if (Test-Path $pidPath) { $timerPid=Get-Content $pidPath -ErrorAction SilentlyContinue; if ($timerPid) { Stop-Process -Id ([int]$timerPid) -Force -ErrorAction SilentlyContinue }; Remove-Item $pidPath -Force -ErrorAction SilentlyContinue }" }], rebootRequired: false },
  'disable-advertising-id': { commands: [{ description: 'Re-enabling Windows Advertising ID', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-tailored-experiences': { commands: [{ description: 'Re-enabling tailored experiences', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-activity-history': { commands: [{ description: 'Re-enabling activity history', script: "$path='HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System'; Set-ItemProperty -Path $path -Name PublishUserActivities -Value 1 -Type DWord -Force; Set-ItemProperty -Path $path -Name UploadUserActivities -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'disable-location-tracking': { commands: [{ description: 'Re-enabling location tracking', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location' -Name Value -Value 'Allow' -Force" }], rebootRequired: true },
  'disable-windows-feedback': { commands: [{ description: 'Re-enabling Windows feedback prompts', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-llmnr': { commands: [{ description: 'Re-enabling LLMNR', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\DNSClient' -Name EnableMulticast -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'menu-delay': { commands: [{ description: 'Restoring menu delay', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MenuShowDelay -Value '400' -Force" }], rebootRequired: false },
  'inactive-window-scroll': { commands: [{ description: 'Re-enabling inactive window scrolling', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name MouseWheelRouting -Value 2 -Type DWord -Force" }], rebootRequired: false },
  'disable-sticky-keys': { commands: [{ description: 'Restoring Sticky Keys shortcut', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Accessibility\\StickyKeys' -Name Flags -Value '510' -Force" }], rebootRequired: false },
  'disable-usb-suspend': { commands: [{ description: 'Re-enabling USB selective suspend', script: 'powercfg /setacvalueindex SCHEME_CURRENT SUB_USB USBSELECTIVE 1; powercfg /setactive SCHEME_CURRENT' }], rebootRequired: false },
  'show-file-extensions': { commands: [{ description: 'Hiding file extensions', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -Value 1 -Type DWord -Force; Stop-Process -Name explorer -Force" }], rebootRequired: false },
  'disable-thumbnails': { commands: [{ description: 'Re-enabling file thumbnails', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -Value 0 -Type DWord -Force; Stop-Process -Name explorer -Force" }], rebootRequired: false },
  'disable-tooltips': { commands: [{ description: 'Re-enabling Explorer tooltips', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-wallpaper-slideshow': { commands: [{ description: 'Restoring wallpaper slideshow', script: "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-system-sounds': { commands: [{ description: 'Re-enabling system sounds', script: "Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -Value '.Current' -Force" }], rebootRequired: false },
  'show-hidden-files': { commands: [{ description: 'Hiding hidden files', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -Value 2 -Type DWord -Force" }], rebootRequired: false },
  'hide-task-view': { commands: [{ description: 'Showing Task View button', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-taskbar-search': { commands: [{ description: 'Restoring taskbar search', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-hibernation': { commands: [{ description: 'Re-enabling hibernation', script: 'powercfg /hibernate on' }], rebootRequired: false },
  'disable-fast-startup': { commands: [{ description: 'Re-enabling Fast Startup', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' -Name HiberbootEnabled -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'enable-hags': { commands: [{ description: 'Restoring GPU scheduling setting', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' -Name HwSchMode -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'disable-bits': { commands: [{ description: 'Re-enabling Background Intelligent Transfer Service', script: "Set-Service -Name BITS -StartupType Manual -ErrorAction Stop; Start-Service -Name BITS -ErrorAction Stop" }], rebootRequired: false },

  // ─── NEW BATCH (2026-08) ──────────────────────────────────────────────
  'disable-widgets': { commands: [{ description: 'Re-enabling Widgets', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Dsh' -Name AllowNewsAndInterests -ErrorAction SilentlyContinue; Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarDa -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-recall': { commands: [{ description: 'Re-enabling Recall and AI activity analysis', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableAIDataAnalysis -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-netbios': { commands: [{ description: 'Restoring NetBIOS over TCP/IP defaults', script: "Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NetBT\\Parameters\\Interfaces' | ForEach-Object { Remove-ItemProperty -Path $_.PSPath -Name NetbiosOptions -ErrorAction SilentlyContinue }" }], rebootRequired: true },
  'disable-smb1': { commands: [{ description: 'Re-enabling SMBv1 server protocol', script: "Set-SmbServerConfiguration -EnableSMB1Protocol $true -Confirm:$false" }], rebootRequired: false },
  'show-seconds-clock': { commands: [{ description: 'Restoring default clock format', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowSecondsInSystemClock -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'hide-meet-now': { commands: [{ description: 'Showing the Meet Now taskbar icon again', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowMeetNow -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-fullscreen-optimizations': { commands: [{ description: 'Restoring fullscreen optimizations', script: "Remove-ItemProperty -Path 'HKCU:\\System\\GameConfigStore' -Name GameDVR_FSOBehavior -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-welcome-experience': { commands: [{ description: 'Re-enabling the Windows welcome experience', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name 'SubscribedContent-310093Enabled' -Value 1 -Type DWord -Force" }], rebootRequired: false },

  // ─── CATEGORY BATCH x10 EACH (2026-08) ────────────────────────────────
  'disable-ceip-tasks': { commands: [{ description: 'Re-enabling CEIP scheduled tasks', script: "$ceipTasks = @(@('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','Consolidator'), @('\\Microsoft\\Windows\\Customer Experience Improvement Program\\','UsbCeip'), @('\\Microsoft\\Windows\\Application Experience\\','Microsoft Compatibility Appraiser'), @('\\Microsoft\\Windows\\Application Experience\\','ProgramDataUpdater')); foreach ($entry in $ceipTasks) { Enable-ScheduledTask -TaskPath $entry[0] -TaskName $entry[1] -ErrorAction SilentlyContinue | Out-Null }" }], rebootRequired: false },
  'disable-last-access-time': { commands: [{ description: 'Restoring system-managed last-access updates', script: "fsutil behavior set disablelastaccess 2 | Out-Null" }], rebootRequired: true },
  'disable-8dot3-names': { commands: [{ description: 'Restoring volume-default short name creation', script: "fsutil behavior set disable8dot3 2 | Out-Null" }], rebootRequired: true },
  'disable-admin-shares': { commands: [{ description: 'Re-enabling default administrative shares', script: "$shareParams = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters'; Remove-ItemProperty -Path $shareParams -Name AutoShareServer -ErrorAction SilentlyContinue; Remove-ItemProperty -Path $shareParams -Name AutoShareWks -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-remote-assistance': { commands: [{ description: 'Re-enabling Remote Assistance invitations', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance' -Name fAllowToGetHelp -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-remote-desktop': { commands: [{ description: 'Allowing Remote Desktop connections again', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server' -Name fDenyTSConnections -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'speedup-shutdown': { commands: [{ description: 'Restoring default shutdown timeouts', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name WaitToKillServiceTimeout -Value '5000' -Force; Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name AutoEndTasks -Value '0' -Force" }], rebootRequired: false },
  'no-auto-reboot-active': { commands: [{ description: 'Restoring automatic reboot policy', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU' -Name NoAutoRebootWithLoggedOnUsers -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-driver-search': { commands: [{ description: 'Restoring Windows Update driver search', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching' -Name SearchOrderConfig -ErrorAction SilentlyContinue" }], rebootRequired: false },

  'flush-arp-cache': { commands: [{ description: 'No persistent ARP setting to restore', script: "Write-Output 'ARP table rebuilds automatically'" }], rebootRequired: false },
  'disable-hotspot-service': { commands: [{ description: 'Re-enabling the Mobile Hotspot service', script: "if (Get-Service -Name icssvc -ErrorAction SilentlyContinue) { Set-Service -Name icssvc -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'require-network-level-auth': { commands: [{ description: 'Relaxing the NLA requirement again', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp' -Name UserAuthentication -Value 0 -Type DWord -Force" }], rebootRequired: false },
  'disable-wpad': { commands: [{ description: 'Re-enabling Web Proxy Auto-Discovery', script: "if (Get-Service -Name WinHttpAutoProxySvc -ErrorAction SilentlyContinue) { Set-Service -Name WinHttpAutoProxySvc -StartupType Manual -ErrorAction Stop }" }], rebootRequired: true },
  'disable-active-probing': { commands: [{ description: 'Re-enabling NCSI connectivity probes', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\NlaSvc\\Parameters\\Internet' -Name EnableActiveProbing -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-peer-name-resolution': { commands: [{ description: 'Re-enabling the Peer Name Resolution service', script: "if (Get-Service -Name PNRPsvc -ErrorAction SilentlyContinue) { Set-Service -Name PNRPsvc -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'restrict-point-and-print': { commands: [{ description: 'Lifting the printer driver install restriction', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\Printers\\PointAndPrint' -Name RestrictDriverInstallationToAdministrators -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-ssdp-discovery': { commands: [{ description: 'Re-enabling SSDP discovery', script: "if (Get-Service -Name SSDPSRV -ErrorAction SilentlyContinue) { Set-Service -Name SSDPSRV -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'disable-upnp-device-host': { commands: [{ description: 'Re-enabling the UPnP Device Host service', script: "if (Get-Service -Name upnphost -ErrorAction SilentlyContinue) { Set-Service -Name upnphost -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'disable-snmp-trap': { commands: [{ description: 'Re-enabling the SNMP Trap service', script: "if (Get-Service -Name SNMPTRAP -ErrorAction SilentlyContinue) { Set-Service -Name SNMPTRAP -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },

  'disable-filter-keys': { commands: [{ description: 'Restoring the Filter Keys shortcut', script: "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Accessibility\\FilterKeys' -Name Flags -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-toggle-keys': { commands: [{ description: 'Restoring the Toggle Keys shortcut', script: "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Accessibility\\ToggleKeys' -Name Flags -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-touch-keyboard-autoinvoke': { commands: [{ description: 'Re-enabling touch keyboard auto-invoke', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\TabletTip\\1.7' -Name EnableDesktopModeAutoInvoke -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-controller-gamebar-chord': { commands: [{ description: 'Restoring the controller Game Bar chord', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\GameBar' -Name UseNexusForGameBarEnabled -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-touchpad-edge-swipes': { commands: [{ description: 'Re-enabling touchpad edge swipes', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name EdgeSwipeEnabled -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-touchpad-threefinger-slide': { commands: [{ description: 'Re-enabling three-finger slide gestures', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\PrecisionTouchPad' -Name ThreeFingerSlideEnabled -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-windows-ink': { commands: [{ description: 'Re-enabling Windows Ink workspace', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\WindowsInkWorkspace' -Name AllowWindowsInkWorkspace -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'numlock-on-boot': { commands: [{ description: 'Restoring default NumLock boot behavior', script: "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -ErrorAction SilentlyContinue; Remove-ItemProperty -Path 'Registry::HKEY_USERS\\.DEFAULT\\Control Panel\\Keyboard' -Name InitialKeyboardIndicators -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-hover-checkboxes': { commands: [{ description: 'Re-enabling hover item checkboxes', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name AutoCheckSelect -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-tablet-input-service': { commands: [{ description: 'Re-enabling the Touch Keyboard and Handwriting service', script: "if (Get-Service -Name TabletInputService -ErrorAction SilentlyContinue) { Set-Service -Name TabletInputService -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },

  'delay-taskbar-thumbnails': { commands: [{ description: 'Restoring instant thumbnail previews', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ExtendedUIHoverTime -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'hide-start-recommended': { commands: [{ description: 'Showing the Start recommendations section again', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer' -Name HideRecommendedSection -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-drag-full-window': { commands: [{ description: 'Showing window contents while dragging again', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name DragFullWindows -Value '1' -Force" }], rebootRequired: false },
  'never-combine-taskbar-icons': { commands: [{ description: 'Restoring default taskbar button grouping', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name TaskbarGlomLevel -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-window-shake': { commands: [{ description: 'Re-enabling the minimize-on-shake gesture', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisableViewShake -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-snap-layouts-flyout': { commands: [{ description: 'Re-enabling the snap layouts flyout', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name EnableSnapAssistFlyout -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-window-arrange-drag': { commands: [{ description: 'Re-enabling drag-to-snap arrangement', script: "Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name WindowArrangementActive -Value '1' -Force" }], rebootRequired: false },
  'disable-spotlight-wallpapers': { commands: [{ description: 'Re-enabling Spotlight lock screen images', script: "$cdmKey = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager'; Set-ItemProperty -Path $cdmKey -Name RotatingLockScreenEnabled -Value 1 -Type DWord -Force; Set-ItemProperty -Path $cdmKey -Name RotatingLockScreenOverlayEnabled -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'hide-copilot-button': { commands: [{ description: 'Showing the Copilot taskbar button again', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot' -Name TurnOffWindowsCopilot -ErrorAction SilentlyContinue" }], rebootRequired: false },

  'optimize-thread-scheduling': { commands: [{ description: 'Restoring default thread scheduling', script: "Remove-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl' -Name Win32PrioritySeparation -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-svchost-split-threshold': { commands: [{ description: 'Restoring per-service svchost splitting', script: "Remove-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control' -Name SvcHostSplitThresholdInKB -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'optimize-ntfs-memory-usage': { commands: [{ description: 'Restoring default NTFS memory usage level', script: "fsutil behavior set memoryusage 1 | Out-Null" }], rebootRequired: true },
  'disable-modern-standby': { commands: [{ description: 'Restoring Modern Standby (S0 idle)', script: "Remove-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name PlatformAoAcOverride -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'disable-edge-startup-boost': { commands: [{ description: 'Re-enabling Microsoft Edge startup boost', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Edge' -Name StartupBoostEnabled -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-automatic-maintenance': { commands: [{ description: 'Re-enabling automatic maintenance', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Schedule\\Maintenance' -Name MaintenanceDisabled -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-app-readiness': { commands: [{ description: 'Re-enabling the App Readiness service', script: "if (Get-Service -Name AppReadiness -ErrorAction SilentlyContinue) { Set-Service -Name AppReadiness -StartupType Manual -ErrorAction Stop }" }], rebootRequired: false },
  'disable-ssl-time-seeding': { commands: [{ description: 'Re-enabling SSL-based time seeding', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\W32Time\\Config' -Name UtilizeSslTimeData -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'max-system-responsiveness': { commands: [{ description: 'Restoring default 20 percent system responsiveness', script: "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile' -Name SystemResponsiveness -Value 20 -Type DWord -Force" }], rebootRequired: false },
  'disable-memory-integrity': { commands: [{ description: 'Re-enabling Memory Integrity (HVCI)', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity' -Name Enabled -Value 1 -Type DWord -Force" }], rebootRequired: true },
  'windowed-games-optimization': { commands: [{ description: 'Restoring default windowed games swap behavior', script: "$gpuPref = 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences'; $settings = (Get-ItemProperty -Path $gpuPref -Name DirectXUserGlobalSettings -ErrorAction SilentlyContinue).DirectXUserGlobalSettings; if ($settings) { if ($settings -match 'SwapEffectUpgradeEnable=\\d+') { $settings = $settings -replace 'SwapEffectUpgradeEnable=\\d+', 'SwapEffectUpgradeEnable=0' } else { $settings = $settings.TrimEnd(';') + ';SwapEffectUpgradeEnable=0' }; Set-ItemProperty -Path $gpuPref -Name DirectXUserGlobalSettings -Value $settings -Force }" }], rebootRequired: false },
  'disable-multiplane-overlay': { commands: [{ description: 'Re-enabling Multi-Plane Overlay', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm' -Name OverlayTestMode -ErrorAction SilentlyContinue" }], rebootRequired: true },
  'static-pagefile': { commands: [{ description: 'Restoring automatic pagefile management', script: "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management' -Name PagingFiles -Value @(\"$env:SystemDrive\\pagefile.sys\") -Type MultiString -Force" }], rebootRequired: true },
  'disable-click-to-do': { commands: [{ description: 'Re-enabling Click to Do', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableClickToDo -ErrorAction SilentlyContinue; Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI' -Name DisableClickToDo -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'disable-paint-ai': { commands: [{ description: 'Re-enabling Paint AI features', script: "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Paint' -Name DisableCocreator -ErrorAction SilentlyContinue; Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Paint' -Name DisableImageGenerator -ErrorAction SilentlyContinue" }], rebootRequired: false },

  'disable-clipboard-history': { commands: [{ description: 'Re-enabling clipboard history', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Clipboard' -Name EnableClipboardHistory -Value 1 -Type DWord -Force" }], rebootRequired: false },
  'disable-clipboard-cloud-sync': { commands: [{ description: 'Re-enabling cloud clipboard upload', script: "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Clipboard' -Name CloudClipboardAutomaticUpload -ErrorAction SilentlyContinue" }], rebootRequired: false },
  'deny-user-account-information': { commands: [{ description: 'Allowing account information access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\userAccountInformation' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-documents-library': { commands: [{ description: 'Allowing Documents library access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\documentsLibrary' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-pictures-library': { commands: [{ description: 'Allowing Pictures library access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\picturesLibrary' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-videos-library': { commands: [{ description: 'Allowing Videos library access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\videosLibrary' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-email-access': { commands: [{ description: 'Allowing email access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\email' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-radios-access': { commands: [{ description: 'Allowing radio control by apps again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\radios' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-human-presence': { commands: [{ description: 'Allowing presence sensor access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\humanPresence' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
  'deny-broad-filesystem': { commands: [{ description: 'Allowing broad filesystem access again', script: "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\broadFileSystemAccess' -Name Value -Value 'Allow' -Force" }], rebootRequired: false },
};

export function isExecutableOptimizationId(id: string): boolean {
  return Boolean(
    realCommands[id] &&
    verificationCommands[id] &&
    (irreversibleOptimizationIds.has(id) || (revertCommands[id] && revertVerificationCommands[id])) &&
    !nonExecutableOptimizationIds.has(id)
  );
}