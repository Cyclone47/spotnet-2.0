$dir = 'd:\sourcecode\reconstructed\Spotnet2\Spotnet'
Get-ChildItem -Path $dir -Filter *.g.cs -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\' } | ForEach-Object {
    Write-Host "Removing: $($_.FullName)"
    Remove-Item -Path $_.FullName -Force
}
