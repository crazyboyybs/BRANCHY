# Contributing / Contribuir com o BRANCHY

[English](#english) | [Português](#português)

## English

BRANCHY's original code uses MIT; contributions must be compatible with that
license. Preserve third-party notices and do not copy GPL code into the MIT
application without first discussing compatibility and licensing with maintainers.

### Environment

- Windows with Visual Studio and .NET and C++ desktop development support;
- .NET 10 SDK, Windows SDK and Windows App SDK compatible with `BRANCHY.csproj`;
- Git available on PATH;
- Python and Pillow only for regenerating assets, not for building the application.

Build `branchy/BRANCHY.slnx` in Visual Studio or through MSBuild in a Developer
PowerShell. See `branchy/README.md` in the public snapshot, or `README.md` if
reading this file from the application directory.

### Changes and validation

- Use resources in `Strings/pt-BR` and `Strings/en-US` for UI text.
- Preserve the existing ViewModel and service patterns.
- Test destructive Git operations only in disposable repositories.
- Report code tests, builds and visual tests separately.
- Do not submit personal repository files, dumps, raw logs, tokens,
  certificates or local publishing settings.
- Review the diff and run `branchy/tools/Test-PublicSource.ps1` before publishing.

When creating commits, configure a public identity or a GitHub no-reply address.
Author and committer names and emails remain in history even when project files
contain no personal data.

---

## Português

O código próprio do BRANCHY é MIT; contribuições devem ser compatíveis com essa
licença. Preserve os avisos de terceiros e não copie código GPL para o aplicativo
MIT sem antes discutir a compatibilidade e a licença com os mantenedores.

### Ambiente

- Windows com Visual Studio e suporte a desenvolvimento .NET e C++ para desktop;
- .NET 10 SDK, Windows SDK e Windows App SDK compatíveis com `BRANCHY.csproj`;
- Git disponível no PATH;
- Python e Pillow apenas para regenerar assets, não para compilar o aplicativo.

Compilar a solução `branchy/BRANCHY.slnx` pelo Visual Studio ou pelo MSBuild de
um Developer PowerShell. Consulte `branchy/README.md` no snapshot público, ou
`README.md` se estiver lendo este arquivo na pasta do aplicativo.

### Mudanças e validação

- Use recursos em `Strings/pt-BR` e `Strings/en-US` para textos da interface.
- Preserve o padrão de ViewModels e serviços existente.
- Teste operações destrutivas do Git somente em repositórios descartáveis.
- Informe separadamente testes de código, compilação e testes visuais.
- Não envie arquivos de repositórios pessoais, dumps, logs brutos, tokens,
  certificados ou configurações locais de publicação.
- Revise o diff e rode `branchy/tools/Test-PublicSource.ps1` antes de publicar.

Ao criar commits, configure uma identidade pública ou um endereço no-reply do
GitHub. O nome e o email do autor e do committer ficam no histórico, mesmo que
os arquivos do projeto não contenham dados pessoais.
