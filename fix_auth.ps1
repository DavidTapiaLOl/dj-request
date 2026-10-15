 = 'C:/Users/juand/dj-requests/backend/src/DjRequests.Api/Modules/Accounts/Endpoints/AuthEndpoints.cs'  
 = Get-Content -Raw -Path   
 =  -replace '\\\\', ''  
 =  -replace '\\/', '/'  
 =  -replace '\\" ", " "'  
[System.IO.File]::WriteAllText(, , [System.Text.Encoding]::UTF8)  
