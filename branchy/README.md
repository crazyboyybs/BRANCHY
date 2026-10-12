# BRANCHY

BRANCHY é um cliente Git open source empacotado com WinUI 3, desenvolvido inicialmente no clone oficial do Git e
orientado pelos fluxos do `git-gui` original. A interface é uma implementação
nova: alterações, revisão e commit aparecem como tarefas sequenciais, sem
reproduzir o layout Tcl/Tk.

O aplicativo executa o Git instalado como processo separado; não depende de
compilar o clone upstream. O código próprio usa [MIT](LICENSE), com
[avisos de terceiros](THIRD-PARTY-NOTICES.md) preservados. Para distribuir apenas
as fontes públicas do BRANCHY, consulte
[o guia de publicação](docs/distributing-open-source.md).

## Estado atual

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

## Estrutura

- `Models`: estado estável retornado pelo Git;
- `Services`: execução segura do `git.exe`, localização e seletor de pasta;
- `ViewModels`: estado e comandos independentes do WinUI;
- `Strings/pt-BR` e `Strings/en-US`: recursos dos pacotes de idioma;
- `ContextMenu`: servidor COM nativo empacotado para os menus do Explorer;
- `MainPage.xaml`: workspace orientado a tarefas.

## Compilar

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

## Instalar a versão 1.0

Baixe o ZIP x64 na [release v1.0](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0)
e siga o [guia de instalação](docs/installing.md). Ele contém o bundle MSIX,
as dependências do Windows App Runtime e somente o certificado público de teste.
O runtime .NET acompanha o aplicativo; o Git deve ser instalado separadamente.

O menu moderno é registrado para uma pasta selecionada e para o plano de
fundo da pasta. O menu clássico é registrado para pastas selecionadas. O
registro é declarativo no manifesto MSIX: não há `regsvr32` nem chaves de
registro permanentes deixadas pelo aplicativo. A Área de Trabalho é excluída
dos dois menus para evitar a criação acidental de um repositório nesse local.

Esta primeira distribuição usa certificado autoassinado para avaliação. Ele
não equivale a assinatura pública confiável nem à aprovação da Microsoft Store.

## Edição e desempenho

A edição rápida altera o arquivo completo da árvore de trabalho e mantém o
índice intacto. Preserva BOM e quebras de linha, aceita UTF-8 e UTF-16 com BOM
até 4 MB, e recusa arquivos externos, links e alterações concorrentes.

A análise de C# e Rust, com medições reproduzíveis, está em
[`docs/performance-and-languages.md`](docs/performance-and-languages.md).
