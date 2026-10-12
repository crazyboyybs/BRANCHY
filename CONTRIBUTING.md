# Contribuir com o BRANCHY

O código próprio do BRANCHY é MIT; contribuições devem ser compatíveis com essa
licença. Preserve os avisos de terceiros e não copie código GPL para o aplicativo
MIT sem antes discutir a compatibilidade e a licença com os mantenedores.

## Ambiente

- Windows com Visual Studio e suporte a desenvolvimento .NET e C++ para desktop;
- .NET 10 SDK, Windows SDK e Windows App SDK compatíveis com `BRANCHY.csproj`;
- Git disponível no PATH;
- Python e Pillow apenas para regenerar assets, não para compilar o aplicativo.

Compilar a solução `branchy/BRANCHY.slnx` pelo Visual Studio ou pelo MSBuild de
um Developer PowerShell. Consulte `branchy/README.md` no snapshot público, ou
`README.md` se estiver lendo este arquivo na pasta do aplicativo.

## Mudanças e validação

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
