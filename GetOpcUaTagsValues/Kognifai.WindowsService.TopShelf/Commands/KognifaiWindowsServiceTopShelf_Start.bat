@echo off
setlocal enabledelayedexpansion

set programpath="%programfiles%\Kognifai\OPCUA_GetStaticTagsService"
set service=Kognifai OPCUA GetTags Service

sc query "%service%" | find "%service%"
if "!errorlevel!"=="0" (
	@echo Starting Service
	cd %programpath%
	Kognifai.WindowsService.TopShelf.exe start
	cd "%programpath%\Commands\"
)
