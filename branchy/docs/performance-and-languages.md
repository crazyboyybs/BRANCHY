# C# e Rust no BRANCHY

## Código atual

A interface WinUI, ViewModels, persistência, localização e o serviço que executa
`git.exe` já são C#/.NET 10. A extensão dos menus do Explorer é C++/COM. O
repositório oficial do Git contém C, Tcl/Tk, scripts e componentes experimentais
Rust; o BRANCHY usa o Git instalado, não compila uma variante própria desse código.

## Medição local

Validação Release em 12/10/2026, no clone de desenvolvimento com alterações
pendentes; mediana de cinco execuções após aquecimento:

| Operação | Tempo |
| --- | ---: |
| `GitService.GetSnapshotAsync` | 134,82 ms |
| `GitService.GetBranchesAsync` | 123,63 ms |
| `GitService.GetHistoryAsync` (até 300 commits) | 1.151,14 ms |
| `GitDiffLine.Parse` (20.001 linhas semânticas) | 1,94 ms |

Esses valores incluem criação dos processos e leitura das saídas; não são um
perfil de CPU nem um benchmark comparativo com Rust. O estado do disco, Defender,
cache, tamanho do repositório e configuração do Git afetam os resultados.
Para reproduzir, a partir da pasta `branchy`:
`dotnet run --project ../branchy-validation/Branchy.Validation.csproj -c Release -- <repositorio>`.

## Decisão por componente

| Componente | Linguagem indicada | Motivo e próximo passo |
| --- | --- | --- |
| UI, navegação, configurações e edição | C# | Já usa WinUI nativamente. Priorizar virtualização, cancelamento e atualizações em lote. |
| Parser e pesquisa de diffs | C# inicialmente | A medição atual é pequena. Reavaliar CPU/alocações com patches maiores antes de criar FFI. |
| Histórico e grafo | C#; Rust como candidato futuro | Paginação, cache e redução de processos primeiro. Avaliar `gix` para leitura em lote de objetos se o perfil justificar. |
| Varredura de arquivos/ignore | C# inicialmente; Rust opcional | Medir grandes árvores e regras de ignore. O custo pode ser de I/O, não de linguagem. |
| Menu do Explorer | C++ ou Rust com `windows` | Pequeno servidor COM já nativo. Rust pode melhorar segurança de memória, mas não há ganho de desempenho demonstrado; C# adicionaria runtime/interop. |
| Commit, merge, rebase, fetch e push | Git oficial | Manter hooks, filtros, credential helpers, configuração e semântica do Git instalado. Substituição exige testes de compatibilidade. |

Nesta alteração o diff principal passou a ser entregue como uma lista inteira,
em vez de emitir uma notificação por linha; seu parser roda fora da thread de UI,
com cancelamento. O viewer usa o scroll interno virtualizado de `ListView` e é
compartilhado com o histórico. Isso trata custos observáveis de apresentação sem
introduzir uma segunda implementação de Git.

Um componente Rust futuro deve expor uma API C em lotes, com contrato explícito
de buffers, comprimento, ownership, cancelamento e erros. Usar `LibraryImport`
em C# e impedir que panics atravessem a fronteira. Copiar milhares de strings
individualmente entre runtimes pode consumir o ganho pretendido.

## Fontes oficiais

- [Perfil de desempenho em .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/profilers)
- [ListView no WinUI](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/listview-and-gridview)
- [Boas práticas de interoperabilidade nativa](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [Gitoxide/gix: implementação e biblioteca Rust](https://github.com/GitoxideLabs/gitoxide)
- [Projeções Rust das APIs Windows](https://github.com/microsoft/windows-rs)

As recomendações por componente são inferências a partir do código local e
dessas fontes, não uma promessa de que Rust será mais rápido em toda operação.
