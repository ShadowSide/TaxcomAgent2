del /F /Q "www\TaxcomAgent2.exe"
rmdir /S /Q "publishing\publish_tmp"
mkdir "publishing\publish_tmp" &&\
"%programfiles(x86)%\7-zip\7z" a "publishing\publish_tmp\TaxcomAgent2.7z" ".\TaxcomAgent2\TaxcomAgent2\bin\Release\netcoreapp2.1\publish\*" &&\
copy /b "publishing\7zS.sfx" + "publishing\config.txt" + "publishing\publish_tmp\TaxcomAgent2.7z" "www\TaxcomAgent2.exe"
::-sfx7zS2.sfx
rmdir /S /Q "publishing\publish_tmp"
cmd
pause