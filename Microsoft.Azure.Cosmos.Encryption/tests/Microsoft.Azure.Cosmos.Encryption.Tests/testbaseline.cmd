@echo off

set COMMAND=
set TEST_OUTPUT_PATH=
set TEST_BASELINE_PATH=
set TEST_BASELINE_SOURCE_PATH=

:parse_parameters_loop
if /I "%1"=="" goto :done_parse_parameters
if /I "%1"=="/?" goto :usage
if /I "%1"=="-?" goto :usage

if /I "%1"=="/diff" set COMMAND=diff
if /I "%1"=="/update" set COMMAND=update

if /I "%1"=="/outputpath" (
  set TEST_OUTPUT_PATH=%2
  shift
)

if /I "%1"=="/baselinepath" (
  set TEST_BASELINE_PATH=%2
  shift
)

if /I "%1"=="/baselinesourcepath" (
  set TEST_BASELINE_SOURCE_PATH=%2
  shift
)
shift
goto :parse_parameters_loop
:done_parse_parameters
if "%COMMAND%"=="" goto :usage

:setup
if "%TEST_BASELINE_PATH%"=="" (
  if EXIST "bin\Debug\netcoreapp2.0\DotNetSDKEncryptionAPI.json" (
    set TEST_BASELINE_PATH=bin\Debug\netcoreapp2.0
  ) else (
    set TEST_BASELINE_PATH=bin\Release\netcoreapp2.0
  )
)

if "%TEST_OUTPUT_PATH%"=="" (
  if EXIST "bin\Debug\netcoreapp2.0\DotNetSDKEncryptionAPIChanges.json" (
    set TEST_OUTPUT_PATH=bin\Debug\netcoreapp2.0
  ) else (
    set TEST_OUTPUT_PATH=bin\Release\netcoreapp2.0
  )
)

if "%TEST_BASELINE_SOURCE_PATH%"=="" (
  set TEST_BASELINE_SOURCE_PATH=%cd%
)

echo Settings:
set TEST_BASELINE_PATH
set TEST_OUTPUT_PATH

:done_setup

@rem ---------------------------------------------------------------------------
:run
if "%COMMAND%"=="diff" call :do_diff
if "%COMMAND%"=="update" call :do_update
goto :eof
@rem ---------------------------------------------------------------------------

@rem ---------------------------------------------------------------------------
@rem PROCEDURE :usage
@rem ---------------------------------------------------------------------------
:usage
echo usage: testbaselines.cmd [options...]
echo options:
echo   /diff                Compares the test outputs with the expected baselines.
echo   /update              Updates the expected baselines. 
echo                        This will copy test outputs and override the expected baselines.
echo   /outputpath ^<path^>   Specifies the test output path.
echo                        If not specified, default path will be used. 
echo   /baselinepath ^<path^> Specifies the expected baselines path.
echo                        If not specified, default path will be used.
echo   /baselinesourcepath ^<path^> Specifies the expected baselines source path / project path.
echo                        If not specified, default path will be used.
goto :eof
@rem ---------------------------------------------------------------------------


@rem ---------------------------------------------------------------------------
@rem PROCEDURE :do_diff
@rem ---------------------------------------------------------------------------
:do_diff
echo Calling windiff...
call windiff "%TEST_BASELINE_PATH%\DotNetSDKEncryptionAPI.json" "%TEST_OUTPUT_PATH%\DotNetSDKEncryptionAPIChanges.json"

echo Done.
goto :eof
@rem ---------------------------------------------------------------------------

@rem ---------------------------------------------------------------------------
@rem PROCEDURE :do_update
@rem ---------------------------------------------------------------------------
:do_update
echo Calling Updating baselines...

echo .tmp >__exclude.tmp
echo xcopy "%TEST_OUTPUT_PATH%\DotNetSDKEncryptionAPIChanges.json" "%TEST_BASELINE_SOURCE_PATH%\DotNetSDKEncryptionAPI.json" /Q /Y
xcopy "%TEST_OUTPUT_PATH%\DotNetSDKEncryptionAPIChanges.json" "%TEST_BASELINE_SOURCE_PATH%\DotNetSDKEncryptionAPI.json" /Q /Y 

echo Done.
goto :eof
@rem ---------------------------------------------------------------------------


