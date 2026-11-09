$ErrorActionPreference = 'Stop'
$source='E:\XRmuseum\phase2小组和个人作业\COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.docx'
$pdf='E:\XRmuseum\phase2小组和个人作业\COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf'
$before=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$wordApp=New-Object -ComObject Word.Application
$wordApp.Visible=$false
$wordApp.DisplayAlerts=0
try {
    $doc=$wordApp.Documents.Open($source,$false,$true)
    try { $doc.ExportAsFixedFormat($pdf,17) }
    finally { $doc.Close(0) }
}
finally { $wordApp.Quit(0) }
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $before) { throw 'Read-only Word export changed DOCX' }
Write-Output ('Exported PDF: '+(Get-Item -LiteralPath $pdf).Length+' bytes')
exit 0
