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
  if EXIST "bin\Debug\netcoreapp2.0\BaselineTest\TestBaseline" (
    set TEST_BASELINE_PATH=bin\Debug\netcoreapp2.0\BaselineTest\TestBaseline
  ) else (
    set TEST_BASELINE_PATH=bin\Release\netcoreapp2.0\BaselineTest\TestBaseline
  )
)

if "%TEST_OUTPUT_PATH%"=="" (
  if EXIST "bin\Debug\netcoreapp2.0\BaselineTest\TestOutput" (
    set TEST_OUTPUT_PATH=bin\Debug\netcoreapp2.0\BaselineTest\TestOutput
  ) else (
    set TEST_OUTPUT_PATH=bin\Release\netcoreapp2.0\BaselineTest\TestOutput
  )
)

if "%TEST_BASELINE_SOURCE_PATH%"=="" (
  set TEST_BASELINE_SOURCE_PATH=%cd%\BaselineTest\TestBaseline
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
@rem PROCEDURE :do_clean
@rem ---------------------------------------------------------------------------
:do_clean
echo Removing test outputs...
rmdir /s /q %TEST_OUTPUT_PATH% >nul
mkdir %TEST_OUTPUT_PATH% >nul 2>&1
del /s /q %TEST_BASELINE_PATH%\*.tmp* >nul
echo Done.
goto :eof
@rem ---------------------------------------------------------------------------

@rem ---------------------------------------------------------------------------
@rem PROCEDURE :do_diff
@rem ---------------------------------------------------------------------------
:do_diff
echo Calling windiff...
call windiff /T "%TEST_BASELINE_PATH%" "%TEST_OUTPUT_PATH%

@rem Experimental code that passes only list of meaningful files to windiff (instead of full folder comparison done through /T option).
@rem Disabled at the moment, to see how well /T works (UI allows hiding left-only files, which hopefully should be enough)
@rem type NUL > _newfile_
@rem type NUL > _difflist_
@rem for /F %%I in ('powershell -Command "(ls -Recurse \"%TEST_OUTPUT_PATH%\") | ForEach-Object { [System.Console]::WriteLine($_.FullName -replace [regex]::escape(\"%TEST_OUTPUT_PATH%\"), \"\") }"') do (
@rem   if exist %TEST_BASELINE_PATH%%%I (
@rem     echo "%TEST_BASELINE_PATH%%%I" "%TEST_OUTPUT_PATH%%%I" 
@rem   ) else (
@rem     echo _newfile_ "%TEST_OUTPUT_PATH%%%I" 
@rem   )
@rem ) >>_difflist_
@rem call windiff -I _difflist_

echo Done.
goto :eof
@rem ---------------------------------------------------------------------------

@rem ---------------------------------------------------------------------------
@rem PROCEDURE :do_update
@rem ---------------------------------------------------------------------------
:do_update
echo Calling Updating baselines...

echo .tmp >__exclude.tmp
echo xcopy "%TEST_OUTPUT_PATH%" "%TEST_BASELINE_SOURCE_PATH%" /Q /S /Y /EXCLUDE:__exclude.tmp
xcopy "%TEST_OUTPUT_PATH%" "%TEST_BASELINE_SOURCE_PATH%" /Q /S /Y /EXCLUDE:__exclude.tmp
del __exclude.tmp >nul

echo Done.
goto :eof
@rem ---------------------------------------------------------------------------

