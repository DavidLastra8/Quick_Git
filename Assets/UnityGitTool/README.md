# Unity Git Tool

Ventana de editor para trabajar con Git y GitHub sin salir de Unity.
Menú: **Tools → Git Tool**.

## Instalación en otro proyecto

### Opción A: Package Manager (recomendada)
1. En Unity: **Window → Package Manager**.
2. Botón **+** → **Add package from git URL...**
3. Pega:
   ```
   https://github.com/DavidLastra8/Test_Tool.git?path=/Assets/UnityGitTool#v1.0.0
   ```
   - `?path=` indica la carpeta del paquete dentro del repositorio.
   - `#v1.0.0` fija la versión. Si lo quitas, se instala lo último de `main`.

También puedes añadirlo a mano en `Packages/manifest.json`:
```json
"com.davidlastra8.unity-git-tool": "https://github.com/DavidLastra8/Test_Tool.git?path=/Assets/UnityGitTool#v1.0.0"
```

> Si el repositorio es **privado**, Unity usa tu git para descargarlo, así que git necesita
> credenciales de GitHub. Con Git for Windows, el Git Credential Manager abre el navegador la
> primera vez para iniciar sesión. Si no te lo pide o falla, haz el repositorio público o
> clónalo una vez desde la terminal para que se guarden las credenciales.

### Opción B: .unitypackage
En este proyecto: clic derecho sobre `Assets/UnityGitTool` → **Export Package...** → Export.
En el otro proyecto: **Assets → Import Package → Custom Package...**
Es más simple, pero las actualizaciones hay que reimportarlas a mano.

> Usa una sola de las dos opciones en cada proyecto. Si el paquete está instalado y además
> hay una copia en `Assets`, Unity dará un error de assembly duplicado.

## Publicar una versión nueva
1. Cambia `"version"` en `package.json` (por ejemplo, `1.1.0`) y anota los cambios en `CHANGELOG.md`.
2. Haz commit y push.
3. Crea la etiqueta y súbela:
   ```
   git tag v1.1.0
   git push origin v1.1.0
   ```
4. En los otros proyectos, cambia `#v1.0.0` por `#v1.1.0` en `Packages/manifest.json`.

## Funciones
- Detecta si el proyecto está en un repositorio git (también desde una subcarpeta) y si git está instalado.
- **Publicar en GitHub**: init + `.gitignore` de Unity + commit inicial + crear el repo + push.
- **Fetch / Pull / Push**. Pull en modo fast-forward, merge o rebase.
- **Commit** con título y descripción.
- **Archivos cambiados**: lista con el tipo de cambio (M/N/A/D/R/!) y un filtro para ocultar los `.meta`.
- **Ramas**: cambiar al instante desde el desplegable, crear y borrar (en local y en GitHub).
- **Nombres corregidos en vivo** (espacios, acentos, caracteres prohibidos) con una vista previa de cómo se verán en GitHub.
- **Ramas protegidas** (`main`/`master`): no se puede hacer push si hay errores de compilación o si el último commit se hizo con errores.

## Requisitos
- Git instalado (https://git-scm.com). Si Unity no lo encuentra, indica la ruta en *Configuración*.
- Tu identidad configurada en git:
  `git config --global user.name "..."` y `git config --global user.email "..."`.
- Un token de GitHub (sección *Cuenta*): un token clásico con el scope `repo`.
  Se guarda en EditorPrefs de tu equipo, así que sirve para todos tus proyectos.

## Arquitectura (MVP + SOLID)
```
Views/        GitToolWindow                  Vista pasiva (EditorWindow)
Presentation/ GitToolPresenter, IGitToolView, GitToolViewState, GitToolDependencies, NamePreview
UseCases/     PublishRepository, CreateCommit, PushGuard
Git/          Servicios sobre la CLI de git, sanitizador de ramas, política de ramas protegidas
Hosting/      Abstracciones del servicio remoto (IRemoteRepositoryHost...)
GitHub/       Implementación para GitHub (API REST, autenticación, nombres de repo)
Unity/        Adaptadores de AssetDatabase, EditorPrefs y del estado de compilación
Core/         Ejecución de procesos, OperationResult, INameSanitizer
Composition/  GitToolComposer: el único sitio que crea las clases concretas
```
