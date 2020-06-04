@echo off
setlocal enabledelayedexpansion

set programpath="%programfiles%\Kognifai\OPCUA_GetTagValuesService"
set service=Kognifai OPCUA GetTags Service

sc query "%service%" | find "%service%"
if "!errorlevel!"=="0" (
  sc query "%service%" | find "STATE" | find "RUNNING"
  @echo Here1
  if "!errorlevel!"=="0" (
		@echo Stopping Service
				
		cd %programpath%
		taskkill /f /im Kognifai.WindowsService.TopShelf.exe
		@echo -----------------------------------------------
		@echo wait for 30 seconds for the service to stop
		@echo -----------------------------------------------
		timeout /t 30 >NUL 
		
		sc query "%service%" | find "STATE" | find "RUNNING"
	    
	    if "!errorlevel!"=="0" (
			@echo Here2
			Kognifai.WindowsService.TopShelf.exe stop
			@echo -----------------------------------------------
			@echo wait for 30 seconds for the service to stop
			@echo -----------------------------------------------
			timeout /t 30 >NUL 
		)
		
		@echo Here3
	    if "!errorlevel!"=="0" (
			@echo Here3
			net stop "%service%"		
			@echo -----------------------------------------------
			@echo wait for 30 seconds for the service to stop
			@echo -----------------------------------------------
  
			timeout /t 30 >NUL  
		)
		
	)	
)