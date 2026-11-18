$ErrorActionPreference = 'Stop'
$submission = 'E:\XRmuseum\phase2小组和个人作业'
$names = @('COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote','COMP5424-Group2-Phase2-ActivityRecord')
$wordApp = New-Object -ComObject Word.Application
$wordApp.Visible = $false
$wordApp.DisplayAlerts = 0
try {
    foreach ($name in $names) {
        $source = Join-Path $submission ($name + '.docx')
        $pdf = Join-Path $submission ($name + '.pdf')
        $before = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $doc = $wordApp.Documents.Open($source, $false, $true)
        try { $doc.ExportAsFixedFormat($pdf, 17) }
        finally { $doc.Close(0) }
        $after = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ($before -ne $after) { throw 'Word changed the read-only DOCX source.' }
        Write-Output ('Exported ' + $name + ': ' + (Get-Item -LiteralPath $pdf).Length + ' bytes')
    }
}
finally { $wordApp.Quit(0) }
exit 0
