# BRANCHY

Cliente Git open source para Windows, com WinUI 3, integração com o Explorer,
histórico visual e planejamento de implementações.

Esta distribuição contém apenas as fontes atuais do BRANCHY e sua validação,
sem o histórico local de desenvolvimento, dados pessoais, certificados ou builds.
O Git deve estar instalado separadamente e disponível no PATH.

## Baixar e instalar

A primeira versão pública é **1.0**, com tag `v1.0` e versão MSIX `1.0.0.0`.
Baixe o [pacote x64](https://github.com/crazyboyybs/BRANCHY/releases/tag/v1.0)
e siga o [guia de instalação](branchy/docs/installing.md).

**Atenção:** o pacote inicial usa certificado de teste autoassinado. A instalação
exige consentimento explícito para confiar no certificado público; não é uma
assinatura pública confiável. Nenhuma chave privada acompanha os downloads.

- [Funcionalidades e compilação](branchy/README.md)
- [Como contribuir](CONTRIBUTING.md)
- [Segurança](SECURITY.md)
- [Licença MIT](LICENSE)
- [Avisos de terceiros](THIRD-PARTY-NOTICES.md)

## Compilar

Abra `branchy/BRANCHY.slnx` no Visual Studio com os workloads .NET e C++ para
desktop. Pelo Developer PowerShell do Visual Studio, na raiz desta distribuição:

```powershell
msbuild .\branchy\BRANCHY.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x64
dotnet run --project .\branchy-validation\Branchy.Validation.csproj -c Release
```

Um pacote gerado localmente precisa de assinatura para instalação. A chave
privada não acompanha o código-fonte; crie seu próprio certificado de testes
ou configure o serviço de assinatura apropriado. O manifesto contém apenas
a identidade técnica de desenvolvimento do aplicativo.

Consulte [o guia de publicação](branchy/docs/distributing-open-source.md)
antes de criar uma release ou um repositório público.
