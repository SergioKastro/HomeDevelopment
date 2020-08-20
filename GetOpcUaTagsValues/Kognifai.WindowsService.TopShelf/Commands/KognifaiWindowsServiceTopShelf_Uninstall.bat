@echo off
setlocal enabledelayedexpansion

set programpath="%programfiles%\Kognifai\OPCUA_GetStaticTagsService"
set service=Kognifai OPCUA GetTags Service

sc query "%service%" | find "%service%"
if "!errorlevel!"=="0" (
  sc query "%service%" | find "STATE" | find "RUNNING"
  @echo Found Service
  if "!errorlevel!"=="0" (
		@echo Stopping Service
		cd %programpath%
		Kognifai.WindowsService.TopShelf.exe stop
		
		@echo -----------------------------------------------
		@echo wait for 30 seconds for the service to stop
		@echo -----------------------------------------------
  
		timeout /t 30 >NUL  
  )	  
)

 REM Uninstall using the topshel command
 @echo Uninstall using the topshelf command
 cd %programpath%
 Kognifai.WindowsService.TopShelf.exe uninstall
  cd "%programpath%\Commands\"