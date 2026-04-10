@echo off
echo Limpiando .vs, bin y obj recursivamente...

rem Elimina las carpetas del disco
for /d /r %%D in (.vs bin obj) do (
    if exist "%%D" (
        echo Borrando %%D
        rmdir /s /q "%%D"
    )
)


echo Limpieza completada.
pause