# BRANCHY

[English](#english) | [Português](#português)

## English

BRANCHY is an open-source packaged Git client built with WinUI 3, initially
developed inside the official Git clone and guided by the original `git-gui`
workflows. Its interface is a new implementation: changes, review and commits
are presented as sequential tasks without reproducing the Tcl/Tk layout.

The application runs the installed Git as a separate process; it does not
require building the upstream clone. Original code uses [MIT](LICENSE), with
[third-party notices](THIRD-PARTY-NOTICES.md#english) preserved. To distribute
only BRANCHY's public sources, see the
[publication guide](docs/distributing-open-source.md#english).

### Current features

- Automatically open or choose a repository;
- Clone a remote or local repository and automatically open the new copy;
- Refresh changes using `git status --porcelain=v2`;
- View diffs for the index, working tree and new files;
- Stage one file or toggle all changes between staged and unstaged;
- Create commits while preserving the installed Git's hooks and configuration;
- List, create, switch, rename and safely delete local branches;
- Add, edit, fetch and remove remotes;
- Stage only selected hunks of a text change;
- Discard unstaged changes or restore deleted files;
- Create and review a managed `.gitignore` section, distinguishing tracked,
  included and ignored files;
- Explore visual history with references, metadata, changed files and per-commit
  diffs, exporting the selected version directly to ZIP;
- Customize language, theme and layout density;
- Access settings on a separate responsive, centered page with automatic saving
  and an animated Back button;
- Navigate between Home and Planning with native Frame slide transitions while
  preserving workspace state;
- Keep Mica on pages, Thin Desktop Acrylic on dialogs and normal Desktop Acrylic
  on tooltips, with native fallback;
- Animate diff expansion/collapse, find lines with Ctrl+F/F3 and animated search
  opening/closing, and edit the working-tree file;
- Run fetch, fast-forward-only pull and push using the configured credential helper;
- Open a folder from Windows 11's modern context menu or through
  **Show more options** in the classic menu.

### Structure

- `Models`: stable state returned by Git;
- `Services`: safe `git.exe` execution, localization and folder selection;
- `ViewModels`: state and commands independent of WinUI;
- `Strings/pt-BR` and `Strings/en-US`: language resource packages;
- `ContextMenu`: packaged native COM server for Explorer menus;
- `MainPage.xaml`: task-oriented workspace.

### Build

Requires the .NET 10 SDK, Visual Studio with .NET and C++ desktop workloads,
Windows SDK and Git on PATH. Open `BRANCHY.slnx` or use Visual Studio's Developer
PowerShell from this directory:

```powershell
msbuild .\BRANCHY.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64
```

The `Properties/PublishProfiles/win-x64.pubxml` profile generates an x64 MSIX
bundle for sideloading. It also builds and includes Explorer's x64 extension.
The test certificate must match the manifest publisher: `CN=BRANCHY`.

No private key is included in the project. The developer must create or supply
a development certificate; generating the MSIX does not automatically make
it installable on other computers.

### Install version 1.0

Download the x64 ZIP from [release v1.0](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0)
and follow the [installation guide](docs/installing.md#english). It contains the
MSIX bundle, Windows App Runtime dependency and only the public test certificate.
The .NET runtime is bundled with the application; Git must be installed separately.

The modern menu is registered for selected folders and folder backgrounds.
The classic menu is registered for selected folders. Registration is declarative
in the MSIX manifest: there is no `regsvr32` or permanent registry registration
left behind by the application. Desktop is excluded from both menus to avoid
accidentally creating a repository there.

This initial distribution uses a self-signed certificate for evaluation. It is
not equivalent to a publicly trusted signature or Microsoft Store approval.

### Editing and performance

Quick editing changes the complete working-tree file and leaves the index intact.
It preserves BOM and line endings, supports UTF-8 and BOM-marked UTF-16 files
up to 4 MB, and rejects external files, links and concurrent changes.

The C# and Rust analysis, with reproducible measurements, is available in
[`docs/performance-and-languages.md`](docs/performance-and-languages.md#english).

---

## Português

BRANCHY é um cliente Git open source empacotado com WinUI 3, desenvolvido inicialmente no clone oficial do Git e
orientado pelos fluxos do `git-gui` original. A interface é uma implementação
nova: alterações, revisão e commit aparecem como tarefas sequenciais, sem
reproduzir o layout Tcl/Tk.

O aplicativo executa o Git instalado como processo separado; não depende de
compilar o clone upstream. O código próprio usa [MIT](LICENSE), com
[avisos de terceiros](THIRD-PARTY-NOTICES.md#português) preservados. Para distribuir apenas
as fontes públicas do BRANCHY, consulte
[o guia de publicação](docs/distributing-open-source.md#português).

### Estado atual

- abrir automaticamente ou escolher um repositório;
- clonar um repositório remoto ou local e abrir automaticamente a nova cópia;
- atualizar alterações com `git status --porcelain=v2`;
- visualizar diffs do índice, da árvore de trabalho e de arquivos novos;
- preparar um arquivo ou alternar todas as alterações entre selecionadas e fora do índice;
- criar commits preservando hooks e configuração do Git instalado;
- listar, criar, trocar, renomear e excluir com segurança branches locais;
- adicionar, editar, buscar e remover repositórios remotos;
- preparar somente trechos selecionados de uma alteração textual;
- descartar alterações não preparadas ou restaurar arquivos excluídos;
- criar e revisar uma seção gerenciada do `.gitignore`, distinguindo arquivos
  rastreados, incluídos e ignorados;
- examinar um histórico visual com referências, metadados, arquivos alterados e diff por commit,
  exportando a versão selecionada diretamente para ZIP;
- personalizar idioma, tema e densidade do layout;
- acessar configurações em uma página independente, responsiva e centralizada, com salvamento automático e botão Voltar animado;
- navegar entre Início e Planejamento com slide nativo do Frame e preservar o estado do workspace;
- manter o Mica nas páginas, Desktop Acrylic Thin nos diálogos e Desktop Acrylic normal nos tooltips, com fallback nativo;
- expandir/recolher o diff com animação, localizar linhas com Ctrl+F/F3 com abertura/fechamento animados da pesquisa e editar o arquivo da árvore de trabalho;
- executar fetch, pull por avanço rápido e push usando o credential helper configurado;
- abrir uma pasta pelo menu de contexto moderno do Windows 11 ou por
  **Mostrar mais opções** no menu clássico.

### Estrutura

- `Models`: estado estável retornado pelo Git;
- `Services`: execução segura do `git.exe`, localização e seletor de pasta;
- `ViewModels`: estado e comandos independentes do WinUI;
- `Strings/pt-BR` e `Strings/en-US`: recursos dos pacotes de idioma;
- `ContextMenu`: servidor COM nativo empacotado para os menus do Explorer;
- `MainPage.xaml`: workspace orientado a tarefas.

### Compilar

Requer .NET 10 SDK, Visual Studio com workloads .NET e C++ para desktop, Windows
SDK e Git no PATH. Abra `BRANCHY.slnx` ou use o Developer PowerShell do Visual
Studio dentro desta pasta:

```powershell
msbuild .\BRANCHY.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64
```

O perfil `Properties/PublishProfiles/win-x64.pubxml` gera um bundle MSIX x64
para sideload. Ele também compila e inclui a extensão x64 usada pelo Explorer.
O certificado usado para testes deve ter o mesmo publisher do manifesto:
`CN=BRANCHY`.

Nenhuma chave privada acompanha o projeto. O certificado de desenvolvimento
precisa ser criado ou fornecido por quem compila; gerar o MSIX não o torna
automaticamente instalável em outros computadores.

### Instalar a versão 1.0

Baixe o ZIP x64 na [release v1.0](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0)
e siga o [guia de instalação](docs/installing.md#português). Ele contém o bundle MSIX,
as dependências do Windows App Runtime e somente o certificado público de teste.
O runtime .NET acompanha o aplicativo; o Git deve ser instalado separadamente.

O menu moderno é registrado para uma pasta selecionada e para o plano de
fundo da pasta. O menu clássico é registrado para pastas selecionadas. O
registro é declarativo no manifesto MSIX: não há `regsvr32` nem chaves de
registro permanentes deixadas pelo aplicativo. A Área de Trabalho é excluída
dos dois menus para evitar a criação acidental de um repositório nesse local.

Esta primeira distribuição usa certificado autoassinado para avaliação. Ele
não equivale a assinatura pública confiável nem à aprovação da Microsoft Store.

### Edição e desempenho

A edição rápida altera o arquivo completo da árvore de trabalho e mantém o
índice intacto. Preserva BOM e quebras de linha, aceita UTF-8 e UTF-16 com BOM
até 4 MB, e recusa arquivos externos, links e alterações concorrentes.

A análise de C# e Rust, com medições reproduzíveis, está em
[`docs/performance-and-languages.md`](docs/performance-and-languages.md#português).
