# C# and Rust / C# e Rust no BRANCHY

[English](#english) | [Português](#português)

## English

### Current code

The WinUI interface, ViewModels, persistence, localization and service that runs
`git.exe` are already C#/.NET 10. Explorer's context-menu extension uses C++/COM.
The official Git repository contains C, Tcl/Tk, scripts and experimental Rust
components; BRANCHY uses the installed Git rather than building its own variant.

### Local measurements

Release validation on October 12, 2026, in the development clone with pending
changes; median of five runs after warm-up:

| Operation | Time |
| --- | ---: |
| `GitService.GetSnapshotAsync` | 134.82 ms |
| `GitService.GetBranchesAsync` | 123.63 ms |
| `GitService.GetHistoryAsync` (up to 300 commits) | 1,151.14 ms |
| `GitDiffLine.Parse` (20,001 semantic lines) | 1.94 ms |

These values include process creation and output reading; they are neither a
CPU profile nor a comparative Rust benchmark. Disk state, Defender, cache,
repository size and Git configuration affect results. To reproduce from the
`branchy` directory:
`dotnet run --project ../branchy-validation/Branchy.Validation.csproj -c Release -- <repository>`.

### Decisions by component

| Component | Suggested language | Rationale and next step |
| --- | --- | --- |
| UI, navigation, settings and editing | C# | Already uses WinUI natively. Prioritize virtualization, cancellation and batched updates. |
| Diff parsing and search | C# initially | Current measured cost is small. Reassess CPU/allocations with larger patches before adding FFI. |
| History and graph | C#; Rust as a future candidate | Pagination, caching and fewer processes first. Evaluate `gix` for batched object reads if profiling justifies it. |
| File/ignore scanning | C# initially; Rust optional | Measure large trees and ignore rules. Cost may come from I/O rather than language. |
| Explorer menu | C++ or Rust with `windows` | Small COM server is already native. Rust may improve memory safety, but no performance gain has been demonstrated; C# would add runtime/interop overhead. |
| Commit, merge, rebase, fetch and push | Official Git | Preserve hooks, filters, credential helpers, configuration and installed Git semantics. Replacements need compatibility tests. |

The main diff is delivered as a complete list rather than one notification per
line; parsing runs off the UI thread with cancellation. The viewer uses
`ListView`'s internal virtualized scrolling and is shared with history. This
addresses observable presentation costs without introducing another Git
implementation.

A future Rust component should expose a batched C API with explicit contracts
for buffers, length, ownership, cancellation and errors. Use `LibraryImport`
in C# and prevent panics from crossing the boundary. Copying thousands of strings
individually between runtimes may consume the intended benefit.

### Official sources

- [.NET performance profiling](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/profilers)
- [WinUI ListView](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/listview-and-gridview)
- [Native interoperability best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [Gitoxide/gix: Rust implementation and library](https://github.com/GitoxideLabs/gitoxide)
- [Rust projections for Windows APIs](https://github.com/microsoft/windows-rs)

Component recommendations are inferences from local code and these sources,
not a promise that Rust will be faster for every operation.

---

## Português

### Código atual

A interface WinUI, ViewModels, persistência, localização e o serviço que executa
`git.exe` já são C#/.NET 10. A extensão dos menus do Explorer é C++/COM. O
repositório oficial do Git contém C, Tcl/Tk, scripts e componentes experimentais
Rust; o BRANCHY usa o Git instalado, não compila uma variante própria desse código.

### Medição local

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

### Decisão por componente

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

### Fontes oficiais

- [Perfil de desempenho em .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/profilers)
- [ListView no WinUI](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/listview-and-gridview)
- [Boas práticas de interoperabilidade nativa](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [Gitoxide/gix: implementação e biblioteca Rust](https://github.com/GitoxideLabs/gitoxide)
- [Projeções Rust das APIs Windows](https://github.com/microsoft/windows-rs)

As recomendações por componente são inferências a partir do código local e
dessas fontes, não uma promessa de que Rust será mais rápido em toda operação.
