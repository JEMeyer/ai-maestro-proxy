# Load environment variables from .env file, ignoring comments
if (Test-Path .env) {
    Get-Content .env | ForEach-Object {
        if ($_ -notmatch '^\s*#') {
            $name, $value = $_ -split '=', 2
            [System.Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim())
        }
    }
}

dotnet run
