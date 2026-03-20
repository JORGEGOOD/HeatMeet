@echo off
echo Limpiando .vs, bin y obj recursivamente...

rem Elimina las carpetas del disco
for /d /r %%D in (.vs bin obj) do (
    if exist "%%D" (
        echo Borrando %%D
        rmdir /s /q "%%D"
    )
)

rem Quita archivos de Git si estás en un repo
git rm -r --cached . >nul 2>&1
git add . 
git commit -m "Limpiando carpetas ignoradas" >nul 2>&1

echo Limpieza completada.
pause