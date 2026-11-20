# Unity Git Tool

Herramienta de Editor para trabajar con Git y GitHub sin salir de Unity.
Menú: **Tools → Git Tool**.

## Funciones
- Detecta si el proyecto está en un repositorio git (también si está en una subcarpeta) y si git está instalado.
- **Publicar en GitHub**: `git init` + `.gitignore` de Unity + commit inicial + crea el repo en GitHub + `origin` + push.
- Solo inicializar un repositorio local, sin GitHub.
- **Fetch / Pull / Push**. Pull en modo fast-forward, merge o rebase. El primer push de una rama configura su upstream.
- Commit de todos los cambios.
- **Ramas**: listar las locales y las remotas, hacer checkout (las remotas se trackean solas) y crear ramas nuevas.
- Guarda las escenas antes de tocar el disco, pausa el auto-refresh de Unity mientras git trabaja y refresca la AssetDatabase al terminar.
- Todo es asíncrono: el editor no se congela y las operaciones se pueden cancelar.

## Requisitos
- Git instalado (https://git-scm.com). Si Unity no lo encuentra, pon la ruta en *Configuración*.
- Una identidad configurada en git:
  `git config --global user.name "..."` y `git config --global user.email "..."`.
- Un token de GitHub, en *Cuenta*:
  - Token clásico: scope `repo`.
  - Fine-grained: permisos *Administration: write* y *Contents: write*.

## Arquitectura (MVP + SOLID)
```
Views/        GitToolWindow        Vista pasiva (EditorWindow). Pinta y emite eventos.
Presentation/ IGitToolView, GitToolPresenter, GitToolViewState
UseCases/     PublishRepositoryUseCase       Orquesta los pasos de publicación
Git/          Inspector, Initializer, Branch, Sync, Commit y Remote services (CLI de git)
Hosting/      IRemoteRepositoryHost, IAccessTokenStore, DTOs de dominio
GitHub/       GitHubRepositoryHost (API REST) y GitHubTokenAuthArgumentsProvider
Unity/        Adaptadores de AssetDatabase, EditorPrefs, Application.OpenURL
Core/         ICommandRunner, ProcessCommandRunner, OperationResult
Composition/  GitToolComposer: el único sitio que hace "new" de clases concretas
```
- **S**: cada servicio tiene una sola responsabilidad (detectar, ramas, sincronizar...).
- **O**: para añadir GitLab basta con implementar `IRemoteRepositoryHost` y `IGitAuthArgumentsProvider`, sin tocar el presentador.
- **L**: cualquier implementación de una interfaz se puede sustituir por otra (los dobles de test incluidos).
- **I**: las interfaces son pequeñas (`IGitSyncService`, `IGitBranchService`...) en vez de un único `IGitService` enorme.
- **D**: el presentador sólo depende de interfaces, nunca de UnityEditor ni de `Process`, así que se puede testear con mocks.

## Notas
- El token no se guarda nunca en `.git/config` ni en la URL del remoto. Se pasa como cabecera HTTP solo a github.com.
  Si no hay token, git usa tu Git Credential Manager. Los remotos SSH usan tus claves SSH.
- Si un pull deja conflictos, se resuelven con tu herramienta habitual. La ventana mostrará los archivos cambiados.
- GitHub rechaza archivos de más de 100 MB. Para assets grandes, configura Git LFS.
