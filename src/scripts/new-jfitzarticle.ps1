param (
    [string]$markdownFile,  #the filename for the newly authored article, ex: my-new-article.md
  [string]$componentName,  #the name to use for the new component
  [string]$articlePath, #the path to use for the new route
  [string]$displayTitle #the title for the article on the articles listing page
)


##generate the new angular component
Write-Output "Generating new component"
$ngCommand = "ng generate component articles/" + $componentName + " --module=app.module --inline-template --inline-style --skip-tests"
Invoke-Expression $ngCommand
Write-Output "Component generated."

##BEGIN update component rendering template
Start-Sleep -Seconds 5
Write-Output "Updating component rendering template"
$pathToNewComponentClass = "src\app\articles\" + $componentName + "\" + $componentName + ".component.ts"
$renderingTemplate = "  template: ``<markdown src=`"assets/article-content/"+ $markdownFile + "`"></markdown>``,"
$componentClassLines = Get-Content $pathToNewComponentClass
$newComponentClassLInes = @()
$indexOfRenderingTemplateLine = 4
$indexOfStylesLine = 9

$index = 0
$componentClassLines | ForEach-Object {

    #left off here - need to escape the ` mark in the template
    if($index -lt $indexOfRenderingTemplateLine -or $index -ge $indexOfStylesLine)
    {
        $newComponentClassLInes += $_ 
    }
    elseif ($index -eq $indexOfRenderingTemplateLine)
    {
        $newComponentClassLInes +=  $renderingTemplate
    }
    
    $index++
}

Set-Content -Path $pathToNewComponentClass -Value $newComponentClassLInes
Write-Output "Updated component rendering template"
##END update rendering template


##BEGIN update app-routing to include a new route for the new component
Write-Output "Adding new route"
$appRoutingModuleFilePath = "src\app\app-routing.module.ts"
$componentClassName = ($componentName[0]).ToString().ToUpper() + $componentName.Substring(1) + "Component"
$importHomeComponentLine = "import { HomeComponent } from './home/home.component';"
$startOfRoutesLine = "const routes: Routes = ["
$newRouteLine = "  {path: 'articles/" + $articlePath + "', component:" + $componentClassName + "},"
$newComponentLine = "import { " + $componentClassName + " } from './articles/" + $componentName + "/" + $componentName + ".component';"

$appRoutingLines = Get-Content $appRoutingModuleFilePath
$newAppRoutingLines = @()

$index = 0
$appRoutingLines | ForEach-Object {
    $newAppRoutingLines += $_ 
    if ($_ -eq $startOfRoutesLine) {
        $newAppRoutingLines += $newRouteLine        
      }   
    elseif ($_ -eq $importHomeComponentLine) {
        $newAppRoutingLines += $newComponentLine
    }
    $index++
}

Set-Content -Path $appRoutingModuleFilePath -Value $newAppRoutingLines
Write-Output "New route added"
##END update app-routing to include a new route for the new component


##BEGIN:  alter the contents of the articles directory json file
# Load the contents of the JSON file into a variable
Write-Output "Updating article directory"
$articleDirectoryFilePath = ".\src\assets\article-directory.json"
$json = Get-Content -Path $articleDirectoryFilePath | Out-String | ConvertFrom-Json

# Modify the contents of the JSON file
$json | Select-Object -Property * | ForEach-Object {
    # Make your desired modifications to the JSON object here
}

$valueToAdd =@"
{
    "urlpath":"$articlePath",
    "title":"$displayTitle",
    "id":"",
    "tagline":"",
    "date":"",
    "src":""
}
"@

$json += (ConvertFrom-Json -InputObject $valueToAdd)

# Write the new contents of the JSON file back to disk
$json | ConvertTo-Json | Set-Content -Path $articleDirectoryFilePath
Write-Output "Updated article directory"
