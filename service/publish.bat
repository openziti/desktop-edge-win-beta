@echo off
REM Copyright NetFoundry, Inc.
REM
REM Licensed under the Apache License, Version 2.0 (the "License");
REM you may not use this file except in compliance with the License.
REM You may obtain a copy of the License at
REM
REM https://www.apache.org/licenses/LICENSE-2.0
REM
REM Unless required by applicable law or agreed to in writing, software
REM distributed under the License is distributed on an "AS IS" BASIS,
REM WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
REM See the License for the specific language governing permissions and
REM limitations under the License.
REM

set CURDIR=%CD%
set SVC_ROOT_DIR=%~dp0

set /p BUILD_VERSION=<%SVC_ROOT_DIR%..\version
IF "%BUILD_VERSION%"=="" GOTO BUILD_VERSION_ERROR

echo mkdir %USERPROFILE%\.ssh and add github.com to known_hosts... 2>&1
mkdir %USERPROFILE%\.ssh 2>&1

echo adding github key: ssh-keyscan -t rsa github.com 2>&1
ssh-keyscan -t rsa github.com >> %USERPROFILE%\.ssh\known_hosts 2>&1

echo looking for key using: ssh-keygen -F github.com - expect to find it now! 2>&1
ssh-keygen -F github.com 2>&1

echo converting shallow clone so travis can co: %GIT_BRANCH%
git remote set-branches origin %GIT_BRANCH% 2>&1
git fetch --depth 1 origin %GIT_BRANCH% 2>&1
git checkout %GIT_BRANCH% 2>&1
CALL :FAIL %ERRORLEVEL% "checkout failed"
echo git checkout %GIT_BRANCH% complete: %ERRORLEVEL%

call %SVC_ROOT_DIR%\build.bat CI
SET ACTUAL_ERR=%ERRORLEVEL%
if %ACTUAL_ERR% NEQ 0 (
    @echo.
    echo call to build.bat failed with %ACTUAL_ERR%
    @echo.
    exit /b 1
) else (
    @echo.
    echo result of ninja build: %ACTUAL_ERR%
)

IF "%GIT_BRANCH%"=="master" GOTO RELEASE
echo Publishing to snapshot repo
ziti-ci publish artifactory --groupId=ziti-tunnel-win.amd64.windows --artifactId=ziti-tunnel-win --version=%BUILD_VERSION%-SNAPSHOT --target=service/ziti-tunnel-win.zip 2>&1
REM ziti-ci publish artifactory --groupId=ziti-tunnel-win.amd64.windows --artifactId=ziti-tunnel-win --version=%BUILD_VERSION%-SNAPSHOT --target=service/ziti-tunnel-win.zip --classifier=%GIT_BRANCH% 2>&1
GOTO END

:RELEASE
echo NO LONGER PUBLISHING TO ARTIFACTORY.
REM echo Publishing release
REM ziti-ci publish artifactory --groupId=ziti-tunnel-win.amd64.windows --artifactId=ziti-tunnel-win --version=%BUILD_VERSION% --target=service/ziti-tunnel-win.zip 2>&1
GOTO END

:BUILD_VERSION_ERROR
echo The build version environment variable was not set - cannot publish
exit /b 1

:FAIL
IF %~1 NEQ 0 (
    echo ================================================================
    @echo.
    echo FAILURE:
    echo     %~2
    @echo.
    echo ================================================================
    exit /b %~1
)
exit /b 0

:END

echo publishing complete - committing version.go as ci
echo changing back to: %CURDIR%
cd %CURDIR%
echo current dir: %CD%

echo configuring git - relies on build.bat successfully grabbing ziti-ci and build.bat updating service/ziti-tunnel/version.go
ziti-ci configure-git 2>&1

REM echo git commit -m "[ci skip] committing updated version information" complete: %ERRORLEVEL%
echo ========================================================
echo issuing git push
echo ========================================================
echo setting git config remote.url git@github.com:openziti/desktop-edge-win.git
git config remote.url git@github.com:openziti/desktop-edge-win.git 2>&1
git config --list 2>&1
dir github_deploy_key
git push --verbose 2>&1
CALL :FAIL %ERRORLEVEL% "git push failed"
echo git push complete: %ERRORLEVEL%
echo publish script has completed
