# Smart Assistant engine (PowerShell 5.1) for WinPE Launcher-NG.
# Reads the login settings from apps-config.json (via the launcher's tolerant parser),
# calls a cloud LLM in the OpenAI-compatible format (chat/completions), prints the reply to stdout (UTF-8).
param(
    [Parameter(Mandatory = $true)][string]$PromptFile,
    [Parameter(Mandatory = $true)][string]$SessionFile,
    [Parameter(Mandatory = $true)][string]$LauncherExe
)

$ErrorActionPreference = 'Stop'

# TLS 1.2 - WinPE and others often default to TLS1.0/1.1 which gets blocked.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

function Say([string]$m) { [Console]::Out.WriteLine($m) }

# Repairs text that was decoded as cp1252 instead of UTF-8: the UTF-8 bytes of
# "->" style glyphs come out as "â†’". Only strings carrying that signature are
# touched; a clean reply (or one that cannot round-trip) is returned unchanged.
function Repair-Mojibake([string]$s) {
    if ([string]::IsNullOrEmpty($s)) { return $s }
    # Lead byte char (â/Ã/...) followed by a char from the cp1252 C1 block.
    $sig = "[\u00e0-\u00ff][\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u017d\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u017e\u0178]"
    if ($s -notmatch $sig) { return $s }
    try {
        $c1252 = [Text.Encoding]::GetEncoding(1252,
            (New-Object Text.EncoderExceptionFallback), (New-Object Text.DecoderExceptionFallback))
        $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
        return $utf8.GetString($c1252.GetBytes($s))
    } catch { return $s }
}

# System prompt: enforces the assistant reply rules.
# Language rule: English ONLY (overrides the language of the question).
$sys = 'You are a WinPE rescue technician assistant (imaging, data recovery, passwords, system info).' +
       ' ALWAYS reply in English ONLY, no matter what language the question is written in.' +
       ' You MAY use light Markdown when it improves clarity: **bold**, bullet lists (- ), numbered lists, `inline code`, and ```code blocks```.' +
       ' Keep replies under 500 words, be direct and to the point, never ramble.'

# --- Read config: uses the launcher's own parser (tolerates missing commas / comments) ---
$cfg = $null
try {
    $asm = [System.Reflection.Assembly]::LoadFrom($LauncherExe)
    $t = $asm.GetType('WinPeLauncher.Services.AppConfig')
    $fl = [System.Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
    $m = $t.GetMethod('GetAssistant', $fl)
    if ($m) { $cfg = $m.Invoke($null, $null) }
} catch { $cfg = $null }

if ($null -eq $cfg) {
    Say 'Endpoint/apiKey is missing in apps-config.json. Fill it in and I will answer right away.'
    exit 0
}
$ifl = [System.Reflection.BindingFlags]'NonPublic,Instance'
$itype = $cfg.GetType()
$endpoint = $itype.GetField('Endpoint', $ifl).GetValue($cfg)
$apiKey   = $itype.GetField('ApiKey', $ifl).GetValue($cfg)
$secret   = $itype.GetField('ApiSecret', $ifl).GetValue($cfg)
$model    = $itype.GetField('Model', $ifl).GetValue($cfg)

if ([string]::IsNullOrWhiteSpace($endpoint) -or [string]::IsNullOrWhiteSpace($apiKey)) {
    Say 'Endpoint or apiKey is missing in apps-config.json. Fill it in and I will answer right away.'
    exit 0
}

$prompt = ''
if (Test-Path -LiteralPath $PromptFile) {
    $prompt = [IO.File]::ReadAllText($PromptFile)
}
$prompt = $prompt.Trim()
if ($prompt.Length -eq 0) {
    Say 'You have not typed anything.'
    exit 0
}

# --- Session history: read the last 20 lines (10 user/assistant pairs) as context ---
$msgs = New-Object System.Collections.Generic.List[object]
$msgs.Add(@{ role = 'system'; content = $sys })
if (Test-Path -LiteralPath $SessionFile) {
    $lines = [IO.File]::ReadAllLines($SessionFile)
    $start = [Math]::Max(0, $lines.Length - 20)
    for ($i = $start; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $o = $line | ConvertFrom-Json
            if ($o.role -and $o.text) { $msgs.Add(@{ role = [string]$o.role; content = (Repair-Mojibake ([string]$o.text)) }) }
        } catch { }
    }
}
$msgs.Add(@{ role = 'user'; content = $prompt })

# --- Call API (OpenAI-compatible) ---
$uri = $endpoint.TrimEnd('/')
if ($uri -notmatch '/chat/completions$') { $uri = $uri + '/chat/completions' }

$bodyObj = @{ model = $model; messages = $msgs.ToArray() }
$body = $bodyObj | ConvertTo-Json -Depth 8
$headers = @{ Authorization = 'Bearer ' + $apiKey }
if (-not [string]::IsNullOrWhiteSpace($secret)) { $headers['X-Api-Secret'] = $secret }

# Optional extra headers from apps-config.json (e.g. OpenCode Go's x-opencode-session).
try {
    $hf = $itype.GetField('Headers', $ifl)
    if ($hf) {
        $hd = $hf.GetValue($cfg)
        if ($hd) {
            foreach ($kv in $hd.GetEnumerator()) {
                if (-not [string]::IsNullOrWhiteSpace([string]$kv.Key)) { $headers[[string]$kv.Key] = [string]$kv.Value }
            }
        }
    }
} catch { }

$reply = $null
try {
    $bytes = [Text.Encoding]::UTF8.GetBytes($body)
    # Invoke-RestMethod guesses the charset from the response header; when the
    # server omits it, PS 5.1 falls back to Latin-1 and UTF-8 multibyte glyphs
    # come out as mojibake ("â†’"). Read the raw bytes and decode as UTF-8
    # ourselves instead.
    $w = Invoke-WebRequest -Uri $uri -Method Post -Headers $headers `
        -ContentType 'application/json; charset=utf-8' -Body $bytes -TimeoutSec 60 -UseBasicParsing
    $json = [Text.Encoding]::UTF8.GetString($w.RawContentStream.ToArray())
    $r = $json | ConvertFrom-Json
    if ($r -and $r.choices -and $r.choices.Length -gt 0) {
        $reply = [string]$r.choices[0].message.content
    }
    if ([string]::IsNullOrWhiteSpace($reply)) {
        $reply = 'Empty API response - check endpoint/model in apps-config.json.'
    }
} catch {
    $reply = 'Connection error: ' + $_.Exception.Message + ' (check network, endpoint, API key.)'
}

# 500-word rule - enforced here too, independent of the model.
$reply = (Repair-Mojibake $reply).Trim()
$w = $reply -split '\s+'
if ($w.Count -gt 500) { $reply = (($w[0..499]) -join ' ') }

# --- Save session (temporary on WinPE: %TEMP% = RAM, gone on reboot) ---
try {
    $enc = New-Object System.Text.UTF8Encoding($false)
    $u = '{"role":"user","text":' + (ConvertTo-Json $prompt) + '}' + [Environment]::NewLine
    $a = '{"role":"assistant","text":' + (ConvertTo-Json $reply) + '}' + [Environment]::NewLine
    [IO.File]::AppendAllText($SessionFile, $u, $enc)
    [IO.File]::AppendAllText($SessionFile, $a, $enc)
} catch { }

Say $reply
