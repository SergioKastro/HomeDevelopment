
@echo off
set programpath="%programfiles%\Kognifai\OPCUA_GetStaticTagsService\"

@echo Installing the service
 cd %programpath%
 Kognifai.WindowsService.TopShelf.exe install
 cd "%programpath%\Commands\"
 
 @echo ----------------------------------------------------
 @echo wait for 20 seconds for the service ready to install
 @echo ----------------------------------------------------

 timeout /t 20 >NUL 