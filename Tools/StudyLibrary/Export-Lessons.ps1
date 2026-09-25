param([string]$SourceDirectory = 'C:\Users\harub\OneDrive\Documents\NCST\DiscreetMath')
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$outputDirectory = Join-Path $projectDirectory 'Assets/Resources/StudyLibrary'
$decks = @(
    @('propositional-logic', 'Prelim_Week 1.pptx'),
    @('truth-tables', 'Prelim_Week 2.pptx'),
    @('rules-of-inference', 'Prelim_Week 3.pptx'),
    @('rules-of-inference', 'Prelim_Week 4.pptx'),
    @('formal-informal-proofs', 'Midterm_Week 6.pptx'),
    @('direct-indirect-proofs', 'Midterm_Week 7.pptx'),
    @('mathematical-induction', 'Midterm_Week 8-9.pptm'),
    @('sets', 'Prefinal_Week 11-12.pptx'),
    @('functions-relations', 'Prefinal_Week 13.pptx'),
    @('graphs', 'Final_Week 16.pptx'),
    @('trees', 'Final_Week 17-18.pptx')
)
$lessons = @{}
$powerPoint = New-Object -ComObject PowerPoint.Application
# Never execute macros in the supplied PPTM.
$powerPoint.AutomationSecurity = 3
try {
    foreach ($deck in $decks) {
        $id = $deck[0]
        $filename = 'IT 105_Discrete Structures 1_2nd Sem_' + $deck[1]
        $directory = Join-Path $outputDirectory $id
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        if (-not $lessons.ContainsKey($id)) { $lessons[$id] = [System.Collections.Generic.List[object]]::new() }
        $presentation = $powerPoint.Presentations.Open((Join-Path $SourceDirectory $filename), -1, 0, 0)
        try {
            foreach ($slide in $presentation.Slides) {
                $index = $lessons[$id].Count + 1
                $pageName = 'page-{0:D3}' -f $index
                $width = 1600
                $height = [int]($width * $presentation.PageSetup.SlideHeight / $presentation.PageSetup.SlideWidth)
                $slide.Export((Join-Path $directory ($pageName + '.png')), 'PNG', $width, $height)
                $lessons[$id].Add(@{ image = 'StudyLibrary/' + $id + '/' + $pageName; source = $filename + ' — slide ' + $slide.SlideIndex })
            }
            Write-Output ($id + ': ' + $presentation.Slides.Count + ' source slides exported')
        } finally { $presentation.Close() }
    }
    foreach ($id in $lessons.Keys) {
        @{ pages = @($lessons[$id].ToArray()) } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $outputDirectory ($id + '.json'))
    }
} finally {
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($powerPoint) | Out-Null
}
