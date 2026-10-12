# BRANCHY 1.0

[English](#english) | [Português](#português)

## English

First public release of BRANCHY, an open-source Git client for Windows built
with WinUI 3. Original code is MIT-licensed, with third-party licenses and notices
preserved.

### Features

- Open and clone repositories, including GitHub repositories, with integration into File Explorer's classic and modern context menus.
- Review diffs, find text, expand the viewer and make quick edits.
- Stage files or hunks, select all changes, discard changes and restore files.
- Create commits, amend with message editing, revert commits, and manage branches, merges, conflicts, tags and remotes.
- Visual history with message, diff or branch search and version export to ZIP.
- Interactive gitignore editor and planning board with progress, tags, documentation and linked commits.
- Portuguese and English languages, themes, adjustable layout, Mica and native transitions.

### Downloads and installation

`BRANCHY-1.0-windows-x64.zip` contains the x64 MSIX bundle, Windows App Runtime
dependency, **public** test certificate and instructions. The .NET runtime is
included; install Git separately. See the
[installation guide](https://github.com/crazyboyybs/BRANCHY/blob/main/branchy/docs/installing.md#english).

**Important:** this release uses a self-signed test certificate, not a publicly
trusted signature. Trusting it requires explicit consent and administrator
permission. No private key, PFX, credential or local development history was
published.

The public version is **1.0**; the MSIX manifest uses `1.0.0.0`. Earlier local
`1.8.x.0` installations are not upgraded by this package.

### Validation

Release x64 build, automated functional tests, publication-protection tests,
MSIX content inspection, and signature and hash verification. These checks do
not replace visual testing or testing on a clean Windows installation.

GitHub-generated source ZIPs are also available below.

---

## Português

Primeira versão pública do BRANCHY, cliente Git open source para Windows com
WinUI 3. Código próprio sob licença MIT, com avisos e licenças de terceiros preservados.

### Funcionalidades

- Abrir e clonar repositórios, inclusive do GitHub, com integração aos menus de contexto clássico e moderno do Explorer.
- Revisar diffs, localizar trechos, expandir a visualização e fazer edições rápidas.
- Preparar arquivos ou trechos, selecionar todas as alterações, descartar e restaurar arquivos.
- Criar commits, fazer amend com edição de mensagem, reverter commits, gerenciar branches, merges, conflitos, tags e remotos.
- Histórico visual com pesquisa por mensagem, diff ou branch e exportação de uma versão para ZIP.
- Editor interativo de gitignore e quadro de planejamento com progresso, tags, documentação e commits associados.
- Idiomas português e inglês, temas, layout ajustável, Mica e transições nativas.

### Downloads e instalação

`BRANCHY-1.0-windows-x64.zip` contém o MSIX bundle x64, a dependência do Windows
App Runtime, o certificado **público** de teste e instruções. O runtime .NET está
incluído; instale o Git separadamente. Consulte
[o guia de instalação](https://github.com/crazyboyybs/BRANCHY/blob/main/branchy/docs/installing.md#português).

**Importante:** esta versão usa certificado de teste autoassinado, não uma
assinatura pública confiável. Confiar nele exige consentimento explícito e
permissão de administrador. Nenhuma chave privada, PFX, credencial ou histórico
local de desenvolvimento foi publicado.

A apresentação pública é **1.0**; o manifesto MSIX usa `1.0.0.0`. Instalações
locais anteriores `1.8.x.0` não são atualizadas por este pacote.

### Validação

Compilação Release x64, testes funcionais automatizados, testes de proteção da
publicação, inspeção do conteúdo MSIX e verificação da assinatura e dos hashes.
Essas verificações não substituem testes visuais nem testes em um Windows limpo.

Os ZIPs de código-fonte gerados pelo GitHub também estão disponíveis abaixo.
